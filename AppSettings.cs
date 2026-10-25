using System.IO.Ports;
using System.Xml.Serialization;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Cấu hình cổng serial - serialize đúng theo <c>Config\SystemSetting.xml</c>
/// của Hipot_GPT9801 để mở lại được file cấu hình cũ nguyên vẹn.
/// </summary>
[XmlRoot("SystemSetting")]
public sealed class SystemSetting
{
    public string configName { get; set; } = "SystemSetting.xml";
    public string OfficeDatPath { get; set; } = @"C:\Program Files\offices\office.dat";

    /* Máy hipot GPT-9800 series */
    public string ComPortGPT { get; set; } = "COM2";
    public int ComBaudrateGPT { get; set; } = 115200;
    public int ComStopBitGPT { get; set; } = 1;
    public string ComParityGPT { get; set; } = "None";
    public int ComDataBitGPT { get; set; } = 8;

    /* Tủ điều khiển (Arduino control board) */
    public string ComPortControl { get; set; } = "AUTO";
    public int ComBaudrateControl { get; set; } = 115200;
    public int ComStopBitControl { get; set; } = 1;
    public string ComParityControl { get; set; } = "None";
    public int ComDataBitControl { get; set; } = 8;

    public CycleTimingSettings Timing { get; set; } = new();

    public Parity ParityGPT => ParseParity(ComParityGPT);
    public Parity ParityControl => ParseParity(ComParityControl);
    public StopBits StopBitsGPT => ComStopBitGPT == 2 ? StopBits.Two : StopBits.One;
    public StopBits StopBitsControl => ComStopBitControl == 2 ? StopBits.Two : StopBits.One;

    public static Parity ParseParity(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "even" => Parity.Even,
        "odd" => Parity.Odd,
        "mark" => Parity.Mark,
        "space" => Parity.Space,
        _ => Parity.None
    };

    public static SystemSetting Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var ser = new XmlSerializer(typeof(SystemSetting));
                using var fs = File.OpenRead(path);
                if (ser.Deserialize(fs) is SystemSetting s)
                {
                    // Older configuration files have no Timing element.
                    if (s.Timing is null || !s.Timing.TryValidate(out _)) s.Timing = new();
                    return s;
                }
            }
        }
        catch { /* file hỏng -> dùng mặc định */ }
        return new SystemSetting();
    }

    public void Save(string path)
    {
        if (!Timing.TryValidate(out string error)) throw new InvalidOperationException(error);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var fs = File.Create(tempPath))
            {
                new XmlSerializer(typeof(SystemSetting)).Serialize(fs, this);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }
}

/// <summary>Các thư mục làm việc - khớp với cấu trúc của Hipot_GPT9801.</summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string ConfigDir => Path.Combine(Root, "Config");
    public string ProgramDir => Path.Combine(Root, "program");
    public string ReportDir => Path.Combine(Root, "Report");
    public string PassrateDir => Path.Combine(Root, "passrate");
    /// <summary>Thư mục log vận hành - nằm trong bin (cạnh file .exe).</summary>
    public string LogDir => Path.Combine(Root, "Log");
    public string SettingFile => Path.Combine(ConfigDir, "SystemSetting.xml");
    public string ProgramPointer => Path.Combine(ConfigDir, "Program.ini");
    public string PasswordFile => Path.Combine(ConfigDir, "pw.org");

    public AppPaths(string? root = null) => Root = root ?? AppContext.BaseDirectory;

    public void Ensure()
    {
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(ProgramDir);
        Directory.CreateDirectory(ReportDir);
        Directory.CreateDirectory(PassrateDir);
        Directory.CreateDirectory(LogDir);
    }

    /// <summary>Tên file chương trình đang chọn, đọc từ Program.ini.</summary>
    public string ReadActiveProgramFile()
    {
        try
        {
            if (File.Exists(ProgramPointer))
            {
                string s = File.ReadAllText(ProgramPointer).Trim();
                if (s.Length > 0) return s;
            }
        }
        catch { }
        return "program.csv";
    }

    public void WriteActiveProgramFile(string fileName)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ProgramPointer, fileName);
    }

    public string ActiveProgramPath => Path.Combine(ProgramDir, ReadActiveProgramFile());
}

/// <summary>Mật khẩu admin - lưu trong Config\pw.org như phiên bản cũ.</summary>
public sealed class PasswordStore
{
    private readonly string _path;
    public PasswordStore(string path) => _path = path;

    public string Read()
    {
        try
        {
            if (File.Exists(_path))
            {
                string s = File.ReadAllText(_path).Trim();
                if (s.Length > 0) return s;
            }
        }
        catch { }
        return "admin";   // mật khẩu mặc định của phiên bản cũ
    }

    public void Write(string password)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, password);
    }
}
