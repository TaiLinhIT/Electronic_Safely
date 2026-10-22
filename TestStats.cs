namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Một điểm kết quả phép thử dùng cho biểu đồ cột xu hướng.
/// Index = số thứ tự phép thử trong ca (_total của MainForm).
/// </summary>
public sealed record TestResultPoint(int Index, double CurrentMa, string Result, DateTime Time);

/// <summary>
/// Thống kê kết quả phép thử trong ca chạy app - NGUỒN DỮ LIỆU cho 2 biểu đồ
/// (cột TOTAL/PASS/FAIL + tròn PASS/FAIL) giống màn hình TEST của Hipot_GPT9801.
///
/// Lớp này KHÔNG dính UI nên test được headlessly ở tools/apptest.
/// </summary>
public sealed class TestStats
{
    /// <summary>Số phép thử tối đa hiển thị trên biểu đồ cột (giữ CA GẦN NHẤT).</summary>
    public const int MaxPoints = 60;

    private readonly List<TestResultPoint> _points = new();

    public int Pass { get; private set; }
    public int Fail { get; private set; }
    public int Total => Pass + Fail;

    /// <summary>Danh sách điểm trên biểu đồ cột (tối đa MaxPoints, phép thử cũ bị bỏ).</summary>
    public IReadOnlyList<TestResultPoint> Points => _points;

    /// <summary>Thêm 1 kết quả. pass=false nhưng Result="ERROR" vẫn tính FAIL cho thống kê.</summary>
    public void Add(int index, double currentMa, string result, DateTime time)
    {
        bool pass = result.Equals("PASS", StringComparison.OrdinalIgnoreCase);
        if (pass) Pass++; else Fail++;

        _points.Add(new TestResultPoint(index, currentMa, result.ToUpperInvariant(), time));

        // Giữ tối đa MaxPoints phép thử gần nhất cho biểu đồ cột.
        while (_points.Count > MaxPoints)
            _points.RemoveAt(0);
    }

    /// <summary>
    /// Nạp lại đếm PASS/FAIL của ca đang chạy từ passrate (giống LoadPassRate của app cũ).
    /// Dùng cho 2 biểu đồ TOTAL/PASS/FAIL + tròn PASS/FAIL.
    /// </summary>
    public void Load(int pass, int fail)
        => Load(pass, fail, Array.Empty<TestResultPoint>());

    /// <summary>
    /// Nạp lại trạng thái từ ca đang chạy (khi mở app): đếm pass/fail từ passrate,
    /// các điểm cột từ báo cáo CSV. Giữ tối đa MaxPoints phép thử gần nhất.
    /// </summary>
    public void Load(int pass, int fail, IEnumerable<TestResultPoint> points)
    {
        Pass = Math.Max(0, pass);
        Fail = Math.Max(0, fail);
        _points.Clear();
        foreach (TestResultPoint p in points) _points.Add(p);
        while (_points.Count > MaxPoints) _points.RemoveAt(0);
    }

    /// <summary>Tỷ lệ đạt (First Pass Yield) theo % của toàn bộ phiên - 0 khi chưa có test nào.</summary>
    public double FpyPercent()
        => Total == 0 ? 0 : (double)Pass / Total * 100;

    /// <summary>Giá trị mA lớn nhất đã đo (để biểu đồ cột tự chặn scale) - 0 khi chưa có test nào.</summary>
    public double MaxCurrentMa()
        => _points.Count == 0 ? 0 : _points.Max(p => p.CurrentMa);

    /// <summary>Xóa toàn bộ (dùng khi đổi quét lại ca / test nội bộ).</summary>
    public void Clear()
    {
        Pass = 0;
        Fail = 0;
        _points.Clear();
    }
}
