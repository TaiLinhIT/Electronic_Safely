using System.Reflection;
using System.Security.Cryptography;
using GPT9000_WinForms_TestSystem;
using LicenseShared;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        _checks++;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        var now = DateTimeOffset.UtcNow;
        string fingerprint = MachineFingerprint.GetCurrent();
        string deviceId = MachineFingerprint.GetShortDeviceId();
        var secret = RandomNumberGenerator.GetBytes(32);
        using var rsa = RSA.Create(2048);
        string keyId = LicenseCrypto.ComputeKeyId(rsa.ExportSubjectPublicKeyInfo());
        string bundle = PublicKeyBundleSerializer.Serialize(new PublicKeyBundle
        {
            Keys = new() { new() { ProductId = "TEST", KeyId = keyId, PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem() } }
        });
        string dir = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "office.dat");
        var service = new OfficeLicenseService(file, "TEST", Convert.ToBase64String(secret), bundle);
        LicensePayload Payload() => new()
        {
            ProductId = "TEST", DeviceId = fingerprint, KeyId = keyId,
            IssuedAtUtc = now.AddDays(-1), NotBeforeUtc = now.AddHours(-1), ExpiresAtUtc = now.AddHours(1)
        };
        byte[] Seal(LicensePayload payload, bool badSignature = false)
        {
            byte[] data = LicenseCodec.SerializePayload(payload);
            byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            if (badSignature) signature[0] ^= 1;
            return OfficeLicenseFile.Create(LicenseCrypto.SerializeEnvelope(new SignedLicense
            {
                Algorithm = "RSA-2048-PSS-SHA256", KeyId = keyId,
                PayloadBase64 = Convert.ToBase64String(data), SignatureBase64 = Convert.ToBase64String(signature)
            }), "TEST", payload.DeviceId, secret);
        }

        Check(!service.CanStartTest(), "missing file denies start");
        var valid = Seal(Payload());
        Check(service.Validate(valid, now), "signed RSA license accepts current machine");
        var p = Payload(); p.ExpiresAtUtc = now.AddMinutes(-1);
        var expired = Seal(p);
        Check(!service.Validate(expired, now), "expired denies start");
        File.WriteAllBytes(file, expired);
        Check(!service.CanStartTest(), "expired file denies start");
        File.WriteAllBytes(file, valid);
        Check(service.CanStartTest(), "renewed file takes effect in same service instance");
        using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(!service.CanStartTest(), "locked file denies start without throwing");
        p = Payload(); p.NotBeforeUtc = now.AddSeconds(1);
        Check(!service.Validate(Seal(p), now), "future license denied without early tolerance");
        p = Payload(); p.LicenseType = LicenseType.Perpetual; p.ExpiresAtUtc = now.AddYears(-1);
        Check(service.Validate(Seal(p), now), "perpetual license accepts");
        p = Payload(); p.BindingMode = DeviceBindingMode.ManualDeviceId; p.DeviceId = deviceId;
        Check(service.Validate(Seal(p), now), "short device ID binding accepts");
        foreach (string mac in MacAddress.GetCurrent().Take(1))
        {
            p = Payload(); p.BindingMode = DeviceBindingMode.MacAddress; p.DeviceId = mac;
            Check(service.Validate(Seal(p), now), "MAC binding accepts local adapter");
            p.BindingMode = DeviceBindingMode.ManualDeviceId;
            p.DeviceId = string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
            Check(service.Validate(Seal(p), now), "legacy manual MAC binding accepts local adapter");
            p.DeviceId = "02-00-00-00-00-01";
            if (!MacAddress.Matches(p.DeviceId, MacAddress.GetCurrent()))
                Check(!service.Validate(Seal(p), now), "legacy manual MAC for another machine denied");
        }
        p = Payload(); p.DeviceId = new string('0', 64);
        Check(!service.Validate(Seal(p), now), "other machine denies");
        p = Payload(); p.ProductId = "OTHER";
        Check(!service.Validate(Seal(p), now), "signed wrong product denies");
        Check(!service.Validate(Seal(Payload(), true), now), "valid encryption with forged signature denies");
        var tampered = (byte[])valid.Clone(); tampered[^1] ^= 1;
        Check(!service.Validate(tampered, now), "tampered GCM tag denies");
        Check(!service.Validate(valid[..40], now), "truncated file denies");
        var wrongKey = new OfficeLicenseService(file, "TEST", Convert.ToBase64String(secret), "{}");
        Check(!wrongKey.Validate(valid, now), "unknown signing key denies");
        var wrongSecret = new OfficeLicenseService(file, "TEST", Convert.ToBase64String(new byte[32]), bundle);
        Check(!wrongSecret.Validate(valid, now), "wrong vendor secret denies");
        Check(service.Validate(valid, Payload().ExpiresAtUtc), "expiry boundary inclusive");
        Check(!service.Validate(valid, Payload().ExpiresAtUtc.AddTicks(1)), "one tick beyond expiry denies");
        p = Payload(); p.LicenseType = (LicenseType)255;
        Check(!service.Validate(Seal(p), now), "unknown license type denies");
        using (var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            string ecId = LicenseCrypto.ComputeKeyId(ec.ExportSubjectPublicKeyInfo());
            p = Payload(); p.KeyId = ecId;
            byte[] data = LicenseCodec.SerializePayload(p);
            var envelope = new SignedLicense { KeyId = ecId, Algorithm = "ECDSA-P256-SHA256",
                PayloadBase64 = Convert.ToBase64String(data), SignatureBase64 = Convert.ToBase64String(
                    ec.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
            string ecBundle = PublicKeyBundleSerializer.Serialize(new PublicKeyBundle { Keys = new() {
                new() { ProductId = "TEST", KeyId = ecId, PublicKeyPem = ec.ExportSubjectPublicKeyInfoPem() } } });
            var ecService = new OfficeLicenseService(file, "TEST", Convert.ToBase64String(secret), ecBundle);
            Check(ecService.Validate(OfficeLicenseFile.Create(LicenseCrypto.SerializeEnvelope(envelope),
                "TEST", fingerprint, secret), now), "ECDSA signed license accepts");
        }

        var access = new SettingsAccess();
        Check(!access.Unlocked && !access.IsRoot, "settings start locked");
        Check(!access.Unlock("wrong") && !access.Unlocked, "incorrect password denied");
        Check(access.Unlock("123456") && !access.IsRoot, "normal password has no root visibility");
        Check(access.Unlock("root123456") && access.IsRoot, "root password grants both permissions");
        access.Lock();
        Check(!access.Unlocked && !access.IsRoot, "lock removes both permissions");

        // Instantiate without showing the form or running its Load event (no serial I/O).
        Application.EnableVisualStyles();
        using (var form = new MainForm())
        {
            object Field(string name) => typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            object? Call(string name, params object[] values) => typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, values);
            var password = (TextBox)Field("_settingsPassword");
            password.Text = "root123456"; Call("ToggleSettingsLock");
            Check(((SettingsAccess)Field("_settingsAccess")).IsRoot && ((Button)Field("_btnSaveSettings")).Enabled,
                "form root unlock enables settings");
            Check((bool)Call("SaveSettings", true)!, "form settings saved");
            Check(!((SettingsAccess)Field("_settingsAccess")).Unlocked
                && !((GroupBox)Field("_grpOfficeLicense")).Visible
                && !((Button)Field("_btnSaveSettings")).Enabled, "save locks settings and hides office panel");
            // Force denial independently of the installed production license.
            typeof(MainForm).GetField("_license", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, wrongSecret);
            var log = (RichTextBox)Field("_log");
            string previous = log.Text;
            Call("StartTest", false);
            Call("OnBoardLine", "*TESTON#");
            Check(log.Text == previous && !(bool)Field("_running"), "denied screen and board starts are silent");
            Call("Shutdown");
        }

        if (args.Length > 0)
        {
            var production = new OfficeLicenseService();
            bool validExport = production.Validate(File.ReadAllBytes(args[0]), DateTimeOffset.UtcNow);
            Console.WriteLine("REAL EXPORT valid on this machine now: " + validExport);
            string Resource(string name)
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Licensing." + name)!;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Trim();
            }
            var vendorSecret = Convert.FromBase64String(Resource("office-secret.txt"));
            bool opened = OfficeLicenseFile.TryOpenForThisMachine(File.ReadAllBytes(args[0]),
                Resource("product-id.txt"), vendorSecret, out var json, out var error);
            if (!opened)
                foreach (string mac in MacAddress.GetCurrent())
                {
                    string binding = string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                    if (OfficeLicenseFile.TryOpen(File.ReadAllBytes(args[0]), Resource("product-id.txt"),
                        binding, vendorSecret, out json, out error)) { opened = true; break; }
                }
            CryptographicOperations.ZeroMemory(vendorSecret);
            Console.WriteLine("REAL EXPORT decrypt: " + opened + " " + error);
            if (opened && LicenseCrypto.TryDeserializeEnvelope(json, out var env, out _) && env is not null)
            {
                var keys = PublicKeyBundleSerializer.Deserialize(Resource("public-keys.json"))!;
                var key = keys.Keys.FirstOrDefault(k => k.KeyId.Equals(env.KeyId, StringComparison.OrdinalIgnoreCase));
                if (key is not null && LicenseCrypto.Verify(env, key.PublicKeyPem, out var payload, out error))
                    Console.WriteLine($"REAL EXPORT verified; Product={payload!.ProductId}; NotBefore={payload.NotBeforeUtc:O}; Expires={payload.ExpiresAtUtc:O}; Binding={payload.BindingMode}");
                else Console.WriteLine("REAL EXPORT signature: " + (key is null ? "unknown key " + env.KeyId : error));
            }
        }
        Console.WriteLine($"{_checks} checks passed.");
    }
}
