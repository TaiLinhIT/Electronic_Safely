using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// BỂU ĐỒ CỘT XU HƯỚNG - mô phỏng biểu đồ cột bên trái màn hình TEST của chương trình
/// Hipot_GPT9801: mỗi cột = 1 phép thử, chiều cao = dòng điện ĐO THẬT (mA).
///   - Cột XANH LÁ  = PASS, cột ĐỎ = FAIL
///   - Đường đứt vàng = ngưỡng HIGH SET của chương trình đang chạy
///   - Trục dọc tự co giãn theo giá trị lớn nhất (giống trục 8/18/28/38/48 của app cũ)
/// Vẽ tay bằng GDI+ ở sự kiện Paint -> không cần thư viện ngoài (tương thích .NET 8).
/// </summary>
public sealed class TrendBarChart : Control
{
    private IReadOnlyList<TestResultPoint> _data = Array.Empty<TestResultPoint>();
    private double _highSetMa;

    private static readonly Color PassColor = Color.FromArgb(34, 139, 34);
    private static readonly Color FailColor = Color.FromArgb(178, 34, 34);
    private static readonly Color HiColor = Color.FromArgb(217, 119, 6);
    private static readonly Color GridColor = Color.FromArgb(226, 232, 240);
    private static readonly Color AxisColor = Color.FromArgb(100, 116, 139);

    public TrendBarChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 7.5F);
        Padding = new Padding(38, 12, 8, 18);
    }

    /// <summary>Dữ liệu nguồn (TestStats.Points) + ngưỡng High Set hiện hành.</summary>
    public void SetData(IReadOnlyList<TestResultPoint> data, double highSetMa)
    {
        _data = data ?? Array.Empty<TestResultPoint>();
        _highSetMa = highSetMa;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        RectangleF plot = new(Padding.Left, Padding.Top,
            Math.Max(10, Width - Padding.Left - Padding.Right),
            Math.Max(10, Height - Padding.Top - Padding.Bottom));

        // ---- scale dọc: tròn chẵn, phủ qua cả ngưỡng High Set ----
        double maxData = Math.Max(_highSetMa, 0);
        foreach (TestResultPoint p in _data) maxData = Math.Max(maxData, p.CurrentMa);
        double vmax = Math.Max(1, maxData * 1.2);
        double step = NiceStep(vmax / 5);
        vmax = step * 5;

        // ---- lưới ngang + nhãn trục Y ----
        using Font axisFont = new("Consolas", 7.5F);
        for (int i = 0; i <= 5; i++)
        {
            double v = step * (5 - i);
            float y = plot.Top + plot.Height * i / 5f;
            using var pen = new Pen(GridColor);
            g.DrawLine(pen, plot.Left, y, plot.Right, y);
            string lbl = v % 1 == 0 ? ((int)v).ToString() : v.ToString("0.0");
            SizeF sz = g.MeasureString(lbl, axisFont);
            using var brush = new SolidBrush(AxisColor);
            g.DrawString(lbl, axisFont, brush, plot.Left - sz.Width - 3, y - sz.Height / 2);
        }

        if (_data.Count == 0)
        {
            using var gray = new SolidBrush(Color.FromArgb(203, 213, 225));
            DrawCentered(g, "Chua co phap thu nao", plot, gray);
            return;
        }

        // ---- cột dữ liệu ----
        float slot = plot.Width / Math.Max(1, _data.Count);
        float barW = Math.Max(3, Math.Min(28f, slot * 0.72f));
        using var passBrush = new SolidBrush(PassColor);
        using var failBrush = new SolidBrush(FailColor);
        for (int i = 0; i < _data.Count; i++)
        {
            TestResultPoint p = _data[i];
            float h = (float)(p.CurrentMa / vmax * plot.Height);
            h = Math.Clamp(h, 1.5f, plot.Height);
            float x = plot.Left + slot * i + (slot - barW) / 2;
            float y = plot.Bottom - h;
            g.FillRectangle(p.IsPass() ? passBrush : failBrush, x, y, barW, h);
        }

        // ---- đường ngưỡng HIGH SET ----
        if (_highSetMa > 0 && _highSetMa <= vmax)
        {
            float yHi = plot.Bottom - (float)(_highSetMa / vmax * plot.Height);
            using var dash = new Pen(HiColor) { DashStyle = DashStyle.Dash, Width = 1.6f };
            g.DrawLine(dash, plot.Left, yHi, plot.Right, yHi);
            using var hiBrush = new SolidBrush(HiColor);
            g.DrawString($"Hi set {_highSetMa:0.#} mA", axisFont, hiBrush, plot.Right - 84, yHi - 13);
        }

        // ---- trục đỚi + nhãn phép thử gần nhất ----
        using var axisPen = new Pen(AxisColor);
        g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        using var dark = new SolidBrush(AxisColor);
        DrawCentered(g, $"#{_data[^1].Index} - {_data[^1].CurrentMa:0.000} mA - {_data[^1].Result}",
            new RectangleF(plot.Left, plot.Bottom, plot.Width, 16), dark);
    }

    private static double NiceStep(double rough)
    {
        double[] nice = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5,
                          1, 2, 5, 10, 20, 50, 100 };
        foreach (double n in nice) if (n >= rough) return n;
        return Math.Ceiling(rough);
    }

    private static void DrawCentered(Graphics g, string text, RectangleF area, Brush brush)
    {
        using Font f = new("Segoe UI", 9F, FontStyle.Bold);
        SizeF sz = g.MeasureString(text, f);
        g.DrawString(text, f, brush, area.Left + (area.Width - sz.Width) / 2,
                                  area.Top + (area.Height - sz.Height) / 2);
    }
}

/// <summary>
/// BỂU ĐỒ TRÒN TỶ LỆ ĐẠT (FPY) - mô phỏng vòng tròn đổng bên phải màn hình TEST
/// của chương trình Hipot_GPT9801: miếng XANH = PASS, ĐỎ = FAIL, giữa hiện % đạt.
/// </summary>
public sealed class YieldPieChart : Control
{
    private double _fpy;
    private int _pass, _fail;

    private static readonly Color PassColor = Color.FromArgb(34, 139, 34);
    private static readonly Color FailColor = Color.FromArgb(178, 34, 34);

    public YieldPieChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8F);
        Padding = new Padding(10, 10, 90, 10);   // chừa phải cho chú giải
    }

    public void SetData(int pass, int fail, double fpyPercent)
    {
        _pass = pass;
        _fail = fail;
        _fpy = fpyPercent;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Padding p = Padding;
        float size = Math.Min(Math.Max(20, Height - p.Top - p.Bottom),
                              Math.Max(20, Width - p.Left - p.Right - 60));
        RectangleF pie = new(p.Left, p.Top + ((Height - p.Top - p.Bottom) - size) / 2, size, size);

        float sweep = _total() == 0 ? 360f : (float)(_pass * 360.0 / _total());
        using var failBrush = new SolidBrush(FailColor);
        using var passBrush = new SolidBrush(PassColor);
        using var whitePen = new Pen(Color.White, 1.5f);

        // viền ngoài: FAIL, sau đó phủ PASS theo sweep, rồi khoét lỗ giữa cho FPY.
        g.DrawPie(whitePen, pie, sweep - 90f, 360f - sweep);
        g.FillPie(failBrush, pie, sweep - 90f, 360f - sweep);
        g.FillPie(passBrush, pie, -90f, sweep);
        g.FillEllipse(Brushes.White, pie.Left + size * 0.28f, pie.Top + size * 0.28f,
                      size * 0.44f, size * 0.44f);

        using Font big = new("Segoe UI", 13F, FontStyle.Bold);
        using Font small = new("Segoe UI", 7.5F);
        using var green = new SolidBrush(PassColor);
        string pct = _total() == 0 ? "-" : $"{_fpy:0.0}%";
        SizeF ps = g.MeasureString(pct, big);
        g.DrawString(pct, big, green,
            pie.Left + (size - ps.Width) / 2, pie.Top + (size - ps.Height) / 2 - 6);
        string sub = _total() == 0 ? "Chua test" : $"{_pass}/{_total()} PASS";
        SizeF ss = g.MeasureString(sub, small);
        using var gray = new SolidBrush(Color.FromArgb(100, 116, 139));
        g.DrawString(sub, small, gray,
            pie.Left + (size - ss.Width) / 2, pie.Top + (size - ss.Height) / 2 + 12);

        // ---- chú giải bên phải ----
        float lx = pie.Right + 16;
        g.FillRectangle(passBrush, lx, pie.Top + 14, 12, 12);
        g.DrawString($"PASS  {_pass}", small, Brushes.Black, lx + 16, pie.Top + 12);
        g.FillRectangle(failBrush, lx, pie.Top + 44, 12, 12);
        g.DrawString($"FAIL  {_fail}", small, Brushes.Black, lx + 16, pie.Top + 42);
    }

    private int _total() => _pass + _fail;
}

/// <summary>Extension nhỏ: coi chuỗi PASS (không phân biệt hoa thường) là đạt.</summary>
internal static class TestResultExt
{
    public static bool IsPass(this TestResultPoint p)
        => p.Result.Equals("PASS", StringComparison.OrdinalIgnoreCase);
}
