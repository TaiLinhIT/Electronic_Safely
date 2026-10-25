using System.Security.Cryptography;
using System.Text.Json;
using GPT9000_WinForms_TestSystem;
using LicenseShared;

internal static partial class Program
{
    // Read-only diagnostic. Issuer records may supply the intended binding so an
    // operator can inspect a file issued to another PC; they never grant START.
    private static void InspectLicense(string[] args)
    {
        if (args.Length is < 1 or > 2)
            throw new ArgumentException("Usage: --inspect <office.dat> [issued-licenses.json]");

        string Resource(string name)
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Licensing." + name)
                ?? throw new InvalidOperationException("Missing embedded provisioning.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        }

        byte[] bytes = File.ReadAllBytes(args[0]);
        string product = Resource("product-id.txt");
        var service = new OfficeLicenseService();
        Console.WriteLine("FILE: " + Path.GetFullPath(args[0]));
        Console.WriteLine("SHA256: " + Convert.ToHexString(SHA256.HashData(bytes)));
        Console.WriteLine("START on this PC now: " + service.Validate(bytes, DateTimeOffset.UtcNow));
        var bindings = new List<string> { MachineFingerprint.GetCurrent(), MachineFingerprint.GetShortDeviceId() };
        foreach (string mac in MacAddress.GetCurrent())
        {
            bindings.Add(mac);
            var pairs = Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)).ToArray();
            bindings.Add(string.Join("-", pairs));
            bindings.Add(string.Join(":", pairs));
        }
        if (args.Length == 2)
        {
            using var records = JsonDocument.Parse(File.ReadAllText(args[1]));
            foreach (var record in records.RootElement.EnumerateArray())
                if (record.GetProperty("productId").GetString()?.Equals(product, StringComparison.OrdinalIgnoreCase) == true)
                    bindings.Add(record.GetProperty("deviceId").GetString() ?? "");
        }

        byte[] secret = Convert.FromBase64String(Resource("office-secret.txt"));
        string json = "";
        bool opened = false;
        try
        {
            foreach (string binding in bindings.Distinct(StringComparer.OrdinalIgnoreCase))
                if (OfficeLicenseFile.TryOpen(bytes, product, binding, secret, out json, out _))
                {
                    opened = true;
                    break;
                }
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
        Console.WriteLine("Decrypt with supplied bindings: " + opened);
        if (!opened || !LicenseCrypto.TryDeserializeEnvelope(json, out var envelope, out _) || envelope is null) return;

        var keys = PublicKeyBundleSerializer.Deserialize(Resource("public-keys.json"))!;
        var key = keys.Keys.FirstOrDefault(k => k.ProductId.Equals(product, StringComparison.OrdinalIgnoreCase)
            && k.KeyId.Equals(envelope.KeyId, StringComparison.OrdinalIgnoreCase));
        if (key is null || !LicenseCrypto.Verify(envelope, key.PublicKeyPem, out var payload, out _) || payload is null)
        {
            Console.WriteLine("Trusted signature: False");
            return;
        }
        Console.WriteLine("Trusted signature: True");
        Console.WriteLine($"License: {payload.LicenseId}; Product: {payload.ProductId}; Payload v{payload.SchemaVersion}");
        string deviceId = MachineFingerprint.GetShortDeviceId();
        if (payload.BindingMode == DeviceBindingMode.ManualDeviceId
            && MacAddress.Matches(payload.DeviceId, MacAddress.GetCurrent()))
            deviceId = payload.DeviceId;
        Console.WriteLine("Binding matches this PC: " + LicenseRules.MatchesDevice(payload,
            MachineFingerprint.GetCurrent(), deviceId, MacAddress.GetCurrent()));
        string Local(DateTimeOffset time) => time.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz");
        Console.WriteLine("Main start: " + Local(payload.NotBeforeUtc));
        if (payload.IsPerpetual)
        {
            Console.WriteLine("Main expiry: perpetual");
            return;
        }
        Console.WriteLine("Main expiry (inclusive): " + Local(payload.ExpiresAtUtc));
        foreach (var window in payload.ExtraWindows)
            Console.WriteLine($"Extra [start, end): {Local(window.StartUtc)} -> {Local(window.EndUtc)}");

        // Evaluate time alone, independently of the PC used to inspect the file.
        // No clock change, license rewrite, serial command or measurement occurs.
        bool TimeAllows(DateTimeOffset time) => time >= payload.NotBeforeUtc
            && (payload.IsPerpetual || time <= payload.ExpiresAtUtc || ExtraExperience.Allows(payload.ExtraWindows, time));
        var dates = payload.ExtraWindows.Select(w => w.StartUtc.ToOffset(TimeSpan.FromHours(7)).Date)
            .Prepend(payload.ExpiresAtUtc.ToOffset(TimeSpan.FromHours(7)).Date).Distinct().Order();
        foreach (var date in dates)
            foreach (var clock in new[] { new TimeSpan(20, 29, 59), new TimeSpan(20, 30, 0),
                         new TimeSpan(20, 30, 1), new TimeSpan(21, 29, 59), new TimeSpan(21, 30, 0) })
            {
                var time = new DateTimeOffset(date + clock, TimeSpan.FromHours(7));
                Console.WriteLine($"TIME RULE {Local(time)}: {TimeAllows(time)}");
            }
    }
}
