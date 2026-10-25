using System.Security.Cryptography;
using LicenseShared;

namespace GPT9000_WinForms_TestSystem;

/// <summary>Verifies the current office.dat and its signed main/extra validity periods before START.</summary>
internal sealed class OfficeLicenseService
{
    public static string LicenseDirectory => @"C:\Program Files\offices";
    public static string LicensePath => Path.Combine(LicenseDirectory, "office.dat");

    private readonly string _path;
    private readonly string _productId;
    private readonly string _secret;
    private readonly PublicKeyBundle? _keys;

    public OfficeLicenseService(string? path = null) : this(
        string.IsNullOrWhiteSpace(path) ? LicensePath : path, ReadResource("product-id.txt").Trim(),
        ReadResource("office-secret.txt").Trim(), ReadResource("public-keys.json")) { }

    internal OfficeLicenseService(string path, string productId, string secret, string publicKeys)
    {
        _path = path;
        _productId = productId;
        _secret = secret;
        _keys = PublicKeyBundleSerializer.Deserialize(publicKeys);
    }

    public bool CanStartTest()
        => CanStartTest(out _);

    public bool CanStartTest(out bool expired)
    {
        expired = false;
        try
        {
            // Read again on every START: replacing office.dat takes effect without restarting.
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is < 55 or > (54 + OfficeLicenseFile.MaxPlaintextBytes)) return false;
            var bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            return Validate(bytes, DateTimeOffset.UtcNow, out expired);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.Security.SecurityException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    internal bool Validate(byte[] bytes, DateTimeOffset now)
        => Validate(bytes, now, out _);

    internal bool Validate(byte[] bytes, DateTimeOffset now, out bool expired)
    {
        expired = false;
        byte[]? secret = null;
        try
        {
            if (string.IsNullOrWhiteSpace(_productId) || _keys?.SchemaVersion != 1) return false;
            secret = Convert.FromBase64String(_secret);
            if (!TryOpen(bytes, secret, out var json)
                || !LicenseCrypto.TryDeserializeEnvelope(json, out var envelope, out _)
                || envelope is null) return false;

            var key = _keys.Keys.FirstOrDefault(k =>
                string.Equals(k.ProductId, _productId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(k.KeyId, envelope.KeyId, StringComparison.OrdinalIgnoreCase));
            if (key is null || !LicenseCrypto.Verify(envelope, key.PublicKeyPem, out var payload, out _)
                || payload is null || !Enum.IsDefined(payload.LicenseType)) return false;

            // Strict validity window (no early-start tolerance).
            if (now < payload.NotBeforeUtc
                || (!payload.IsPerpetual && payload.ExpiresAtUtc < payload.NotBeforeUtc)) return false;
            string fingerprint;
            try { fingerprint = MachineFingerprint.GetCurrent(); }
            catch (InvalidOperationException) { fingerprint = ""; }
            string deviceId = fingerprint.Length >= 16 ? "DEV-" + fingerprint[..16] : "";
            // Older issuer versions stored a MAC address under ManualDeviceId.
            // Accept that representation only when it matches an adapter on THIS machine.
            if (payload.BindingMode == DeviceBindingMode.ManualDeviceId
                && MacAddress.Matches(payload.DeviceId, MacAddress.GetCurrent()))
                deviceId = payload.DeviceId;
            var result = LicenseRules.Evaluate(payload, _productId, fingerprint, deviceId, now);
            // An expiry indication is trusted only after signature, product and machine checks.
            expired = result.Status == LicenseStatus.Expired;
            return result.IsValid;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException
            or IOException or InvalidOperationException or System.Security.SecurityException)
        {
            return false;
        }
        finally
        {
            if (secret is not null) CryptographicOperations.ZeroMemory(secret);
        }
    }

    private bool TryOpen(byte[] bytes, byte[] secret, out string json)
    {
        if (OfficeLicenseFile.TryOpenForThisMachine(bytes, _productId, secret, out json, out _)) return true;
        foreach (string mac in MacAddress.GetCurrent())
        {
            var pairs = Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2));
            foreach (string binding in new[] { string.Join("-", pairs), string.Join(":", pairs) })
                if (OfficeLicenseFile.TryOpen(bytes, _productId, binding, secret, out json, out _)) return true;
        }
        return false;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(OfficeLicenseService).Assembly.GetManifestResourceStream("Licensing." + name);
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
