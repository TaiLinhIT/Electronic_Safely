using System.Text;

namespace GPT9000_WinForms_TestSystem;

public sealed class IniConfig
{
    private readonly Dictionary<string, Dictionary<string, string>> _data = new(StringComparer.OrdinalIgnoreCase);
    public string FilePath { get; }

    public IniConfig(string filePath)
    {
        FilePath = filePath;
        if (!File.Exists(filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "");
        }
        Load();
    }

    private void Load()
    {
        _data.Clear();
        string section = "";
        foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line[1..^1].Trim();
                if (!_data.ContainsKey(section)) _data[section] = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            if (!_data.ContainsKey(section)) _data[section] = new(StringComparer.OrdinalIgnoreCase);
            _data[section][line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
    }

    public string Get(string section, string key, string fallback = "")
        => _data.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value : fallback;

    public void Set(string section, string key, string value)
    {
        if (!_data.ContainsKey(section)) _data[section] = new(StringComparer.OrdinalIgnoreCase);
        _data[section][key] = value;
    }

    public void Save()
    {
        var sb = new StringBuilder();
        foreach (var section in _data)
        {
            sb.AppendLine($"[{section.Key}]");
            foreach (var pair in section.Value) sb.AppendLine($"{pair.Key}={pair.Value}");
            sb.AppendLine();
        }
        File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
    }
}
