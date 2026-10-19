using System.Globalization;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Chương trình phép thử - đọc/ghi file <c>program\*.csv</c> theo đúng định dạng
/// của Hipot_GPT9801 (để dùng lại được toàn bộ thư mục program cũ).
///
/// Định dạng file (16 dòng, nhãn xen kẽ giá trị):
///   line 0 : "Model name"        line 1 : &lt;tên&gt;
///   line 2 : "Voltage test (kV)" line 3 : &lt;điện áp&gt;
///   line 4 : "Frequency test (Hz)" line 5 : &lt;tần số&gt;
///   line 6 : "Time test (S)"     line 7 : &lt;thời gian thử&gt;
///   line 8 : "Ramup test (S)"    line 9 : &lt;thời gian ramp&gt;
///   line 10: "Reference test (mA)" line 11: &lt;dòng tham chiếu&gt;
///   line 12: "High set (mA)"     line 13: &lt;ngưỡng trên&gt;
///   line 14: "Low set (mA)"      line 15: &lt;ngưỡng dưới&gt;
/// </summary>
public sealed class TestProgram
{
    public string Name { get; set; } = "program";
    public double VoltageKv { get; set; } = 0.5;
    public double FreqHz { get; set; } = 50;
    public double TimeSec { get; set; } = 2.0;
    public double RampSec { get; set; } = 0.5;
    public double RefMa { get; set; } = 0.0;
    public double HighSetMa { get; set; } = 20.0;
    public double LowSetMa { get; set; } = 0.0;

    /// <summary>Thời gian ước tính cho một chu kỳ: ramp + test + dự phòng.</summary>
    public int EstimatedCycleMs => (int)Math.Clamp((RampSec + TimeSec + 5.0) * 1000, 3000, 120000);

    private static double D(string? s, double fallback)
        => double.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

    /// <summary>Đọc chương trình từ file csv. Trả về null nếu file không đủ 16 dòng.</summary>
    public static TestProgram? Load(string path)
    {
        if (!File.Exists(path)) return null;
        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 16) return null;

        return new TestProgram
        {
            Name = Path.GetFileNameWithoutExtension(path),
            VoltageKv = D(lines[3], 0.5),
            FreqHz = D(lines[5], 50),
            TimeSec = D(lines[7], 2.0),
            RampSec = D(lines[9], 0.5),
            RefMa = D(lines[11], 0.0),
            HighSetMa = D(lines[13], 20.0),
            LowSetMa = D(lines[15], 0.0)
        };
    }

    /// <summary>Ghi chương trình ra file csv đúng định dạng của Hipot_GPT9801.</summary>
    public void Save(string path)
    {
        string I(double v) => v.ToString(CultureInfo.InvariantCulture);
        string[] lines =
        {
            "Model name", Name,
            "Voltage test (kV)", I(VoltageKv),
            "Frequency test (Hz)", I(FreqHz),
            "Time test (S)", I(TimeSec),
            "Ramup test (S)", I(RampSec),
            "Reference test (mA)", I(RefMa),
            "High set (mA)", I(HighSetMa),
            "Low set (mA)", I(LowSetMa)
        };
        File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
    }

    /// <summary>Chương trình mặc định - dùng khi thư mục program chưa có file nào.</summary>
    public static TestProgram CreateDefault() => new();

    public override string ToString()
        => $"{Name}: {VoltageKv} kV / {FreqHz} Hz / {TimeSec} s / ramp {RampSec} s / " +
           $"Hi {HighSetMa} mA / Low {LowSetMa} mA";
}
