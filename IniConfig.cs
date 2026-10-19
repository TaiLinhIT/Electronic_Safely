using System.Text;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Đọc/ghi file INI. Khi ghi lại, các dòng comment (; hoặc #) và dòng trống
/// trong file gốc được GIỮ NGUYÊN (theo từng section), không bị xóa mất.
/// </summary>
public sealed class IniConfig
{
    private sealed class Section
    {
        public readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Comments = new();      // comment đứng trước section header
    }

    private readonly Dictionary<string, Section> _data = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Comment/ghi chú nội tuyến (dòng đầu tiên của file, trước section đầu).</summary>
    public List<string> LeadingComments { get; } = new();
    /// <summary>Ghi chú đứng trước mỗi key, vd "ACW_Low" -> "; Ngưỡng ACW".</summary>
    public Dictionary<string, List<string>> KeyComments { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Ghi chú cuối file.</summary>
    public List<string> TrailingComments { get; } = new();

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

    private static bool IsComment(string line) => line.StartsWith(";") || line.StartsWith("#");

    private void Load()
    {
        _data.Clear();
        KeyComments.Clear();
        LeadingComments.Clear();
        TrailingComments.Clear();

        string section = "";
        bool seenSection = false;
        var pending = new List<string>();     // comment chưa biết thuộc về đâu

        foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0) { pending.Add(""); continue; }
            if (IsComment(line))
            {
                pending.Add(line);
                continue;
            }
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line[1..^1].Trim();
                if (!_data.ContainsKey(section)) _data[section] = new Section();
                _data[section].Comments.AddRange(pending);
                pending.Clear();
                seenSection = true;
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            if (!_data.ContainsKey(section)) _data[section] = new Section();
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            _data[section].Values[key] = value;

            // Gán comment vừa đọc cho key hiện tại
            string ck = $"{section}.{key}";
            if (!KeyComments.ContainsKey(ck)) KeyComments[ck] = new List<string>();
            KeyComments[ck].AddRange(pending);
            pending.Clear();
        }

        // Comment còn sót ở cuối file
        TrailingComments.AddRange(pending);
        if (!seenSection) LeadingComments.AddRange(TrailingComments);
    }

    public string Get(string section, string key, string fallback = "")
        => _data.TryGetValue(section, out var s) && s.Values.TryGetValue(key, out var value) ? value : fallback;

    public void Set(string section, string key, string value)
    {
        if (!_data.ContainsKey(section)) _data[section] = new Section();
        _data[section].Values[key] = value;
    }

    public void Save()
    {
        var sb = new StringBuilder();
        foreach (var section in _data)
        {
            foreach (var c in section.Value.Comments) sb.AppendLine(c);
            sb.AppendLine($"[{section.Key}]");
            foreach (var pair in section.Value.Values)
            {
                string ck = $"{section.Key}.{pair.Key}";
                if (KeyComments.TryGetValue(ck, out var comments))
                    foreach (var c in comments) sb.AppendLine(c);
                sb.AppendLine($"{pair.Key}={pair.Value}");
            }
            sb.AppendLine();
        }
        foreach (var c in TrailingComments) sb.AppendLine(c);
        File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
    }
}
