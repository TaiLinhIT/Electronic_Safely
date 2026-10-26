using Microsoft.Win32;

namespace GPT9000_WinForms_TestSystem;

internal static class SerialPortDiscovery
{
    public static IReadOnlyList<string> Candidates(IEnumerable<string> ports, IEnumerable<string>? excluded = null)
    {
        var bluetooth = new List<string>();
        try
        {
            using var map = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (map is not null)
                foreach (string name in map.GetValueNames())
                    if (name.Contains("BthModem", StringComparison.OrdinalIgnoreCase) && map.GetValue(name) is string port)
                        bluetooth.Add(port);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return Candidates(ports, excluded ?? Array.Empty<string>(), bluetooth);
    }

    internal static IReadOnlyList<string> Candidates(IEnumerable<string> ports, IEnumerable<string> excluded,
        IEnumerable<string> bluetooth)
    {
        var skip = new HashSet<string>(excluded.Concat(bluetooth), StringComparer.OrdinalIgnoreCase);
        // Automatic discovery is for the wired meter/control board. A Bluetooth
        // port can still be selected explicitly without opening every BthModem.
        return ports.Where(p => !skip.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => int.TryParse(p.AsSpan(Math.Min(3, p.Length)), out int n) ? n : int.MaxValue)
            .ToArray();
    }
}
