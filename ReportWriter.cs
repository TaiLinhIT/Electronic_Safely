using System.Globalization;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Ghi báo cáo và thống kê pass rate - đúng định dạng của Hipot_GPT9801
/// để mở lại được toàn bộ dữ liệu báo cáo cũ.
///
/// Report   : <c>Report\{tenChuongTrinh}\{dd-MM-yyyy}_{Ngay|Dem}Report.csv</c>
/// Passrate : <c>passrate\{tenChuongTrinh}\{dd-MM-yyyy}_{Ngay|Dem}.csv</c>
/// </summary>
public sealed class ReportWriter
{
    private readonly string _reportDir;
    private readonly string _passrateDir;
    private readonly int _dayStartHour;
    private readonly int _nightStartHour;

    public ReportWriter(string reportDir, string passrateDir, int dayStartHour = 8, int nightStartHour = 20)
    {
        _reportDir = reportDir;
        _passrateDir = passrateDir;
        _dayStartHour = dayStartHour;
        _nightStartHour = nightStartHour;
    }

    /// <summary>"Ngay" nếu đang trong ca ngày, "Dem" nếu trong ca đêm.</summary>
    public string ShiftName(DateTime t)
        => t.Hour >= _dayStartHour && t.Hour < _nightStartHour ? "Ngay" : "Dem";

    /// <summary>Tên file theo ca: "24-09-2025_Ngay".</summary>
    public string ShiftStamp(DateTime t) => $"{t:dd-MM-yyyy}_{ShiftName(t)}";

    /// <summary>Tiền tố tiêu đề cột - giữ nguyên như bản cũ để Excel mở tương thích.</summary>
    public const string ReportHeader =
        "No,Name,V test(kV),Freq (Hz),Time test (S),Ramup test (S),Ref (mA),Hi set (mA)," +
        "Low set (mA),Voltage result (kV),Current result (mA),Time measure (S),Result";

    /// <summary>
    /// Ghi một dòng kết quả vào báo cáo của ca hiện tại.
    /// Trả về đường dẫn file đã ghi.
    /// </summary>
    public string WriteReport(string programName, int sequenceNo, TestProgram p, HipotResult r, DateTime t)
    {
        string dir = Path.Combine(_reportDir, programName);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{ShiftStamp(t)}Report.csv");

        bool writeHeader = !File.Exists(path);
        var sb = new System.Text.StringBuilder();
        if (writeHeader) sb.AppendLine(ReportHeader);

        // Cột kết quả theo định dạng 5 ký tự của bản cũ: "0.506" / "000.0" / "001.0"
        string kv = r.VoltageKv.ToString("F3", CultureInfo.InvariantCulture);
        string ma = Pad5(r.CurrentMa);
        string sec = Pad5(r.TimeSec);

        sb.Append(sequenceNo).Append(',')
          .Append(programName).Append(',')
          .Append(F(p.VoltageKv)).Append(',')
          .Append(F(p.FreqHz)).Append(',')
          .Append(F(p.TimeSec)).Append(',')
          .Append(F(p.RampSec)).Append(',')
          .Append(F(p.RefMa)).Append(',')
          .Append(F(p.HighSetMa)).Append(',')
          .Append(F(p.LowSetMa)).Append(',')
          .Append(kv).Append(',')
          .Append(ma).Append(',')
          .Append(sec).Append(',')
          .Append(r.Result).Append(',')
          .AppendLine();

        File.AppendAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
        return path;
    }

    /// <summary>Đọc thống kê pass rate của ca hiện tại (tính từ đầu ca).</summary>
    public (int Total, int Pass, int Fail) LoadPassrate(string programName, DateTime t)
    {
        string path = PassratePath(programName, t);
        if (!File.Exists(path)) return (0, 0, 0);
        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 7) return (0, 0, 0);
            return (IntAt(lines, 2), IntAt(lines, 4), IntAt(lines, 6));
        }
        catch { return (0, 0, 0); }
    }

    /// <summary>
    /// Đọc tối đa <paramref name="max"/> phép thử GẦN NHẤT của ca hiện tại từ
    /// báo cáo CSV (cột No / Current result (mA) / Result) - dùng để seed biểu đồ
    /// xu hướng lúc mở app, trình bày tiếp chứ không bắt đầu từ trắng.
    /// Chỉ lấy dòng có Result = PASS/FAIL. Lỗi đọc -> trả về danh sách rỗng.
    /// </summary>
    public IReadOnlyList<TestResultPoint> ReadRecentResults(string programName, DateTime t,
                                                            int max = TestStats.MaxPoints)
    {
        var list = new List<TestResultPoint>();
        string path = Path.Combine(_reportDir, programName, $"{ShiftStamp(t)}Report.csv");
        if (!File.Exists(path)) return list;
        try
        {
            string[] lines = File.ReadAllLines(path);
            foreach (string ln in lines)
            {
                if (string.IsNullOrWhiteSpace(ln) ||
                    ln.StartsWith("No,", StringComparison.OrdinalIgnoreCase)) continue;
                string[] cols = ln.Split(',');
                if (cols.Length < 13) continue;
                string result = cols[12].Trim().ToUpperInvariant();
                if (result is not ("PASS" or "FAIL")) continue;
                if (!int.TryParse(cols[0].Trim(), out int no)) continue;
                double ma = double.TryParse(cols[10].Trim(), NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out double v) ? v : 0;
                list.Add(new TestResultPoint(no, ma, result, t));
            }
        }
        catch { /* báo cáo hỏng - biểu đồ trắng là được */ }
        return list.Count <= max ? list : list.GetRange(list.Count - max, max);
    }

    /// <summary>Ghi đè thống kê pass rate của ca hiện tại.</summary>
    public void SavePassrate(string programName, DateTime t, int total, int pass, int fail)
    {
        string path = PassratePath(programName, t);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string text = $"Model:{programName}\r\nTotal:\r\n{total}\r\nPass:\r\n{pass}\r\nFail:\r\n{fail}";
        File.WriteAllText(path, text);
    }

    private string PassratePath(string programName, DateTime t)
        => Path.Combine(_passrateDir, programName, $"{ShiftStamp(t)}.csv");

    private static int IntAt(string[] lines, int index)
        => int.TryParse(lines[index].Trim(), out int v) ? v : 0;

    private static string F(double v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>Định dạng "000.0" / "015.3" - 5 ký tự, bù số 0 phía trước.</summary>
    private static string Pad5(double v)
    {
        string s = v.ToString("F1", CultureInfo.InvariantCulture);
        return s.Length >= 5 ? s : s.PadLeft(5, '0');
    }
}
