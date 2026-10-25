using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// BIỂU ĐỒ CỘT TỶ LỆ ĐẠT - mô phỏng <c>chart1</c> của chương trình Hipot_GPT9801:
///   - 3 cột TOTAL (xanh dương) / PASS (xanh lá) / FAIL (đỏ)
///   - nhãn số lượng ngay trên đầu mỗi cột (giống series.Label = "#VALY")
///   - dữ liệu lấy từ passrate của ca hiện tại (model + ngày + ca), cập nhật sau mỗi phép thử
/// Vẽ tay bằng GDI+ ở sự kiện Paint -> không cần thư viện ngoài (tương thích .NET 8).
/// </summary>
public sealed class PassRateBarChart : Control
{
    private int _total;
    private int _pass;
    private int _fail;

    private static readonly Color TotalColor = Color.FromArgb(0, 102, 204);
    private static readonly Color PassColor = Color.FromArgb(34, 139, 34);
    private static readonly Color FailColor = Color.FromArgb(200, 38, 38);
    private static readonly Color GridColor = Color.FromArgb(226, 232, 240);
    private static readonly Color AxisColor = Color.FromArgb(100, 116, 139);

    public PassRateBarChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 11F);
        Padding = new Padding(56, 28, 16, 36);
    }

    /// <summary>Cập nhật 3 cột TOTAL / PASS / FAIL của ca hiện tại.</summary>
    public void SetData(int total, int pass, int fail)
    {
        _total = Math.Max(0, total);
        _pass = Math.Max(0, pass);
        _fail = Math.Max(0, fail);
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

        // ---- scale dọc: số nguyên, tối thiểu 0..5; chừa 10% đỉnh để nhãn nằm trên cột ----
        int max = Math.Max(_total, Math.Max(_pass, _fail));
        int roughTop = Math.Max(5, max);
        int step = NiceStep(roughTop / 5.0);
        int vmax = step * 5;
        if (max > vmax * 0.9) vmax += step;   // thêm 1 khoảng cho nhãn #VALY
        int ticks = vmax / step;

        using Font axisFont = new("Consolas", 9.5F);
        using var axisBrush = new SolidBrush(AxisColor);
        using var gridPen = new Pen(GridColor);

        // ---- lưới ngang + nhãn trục Y ----
        for (int i = 0; i <= ticks; i++)
        {
            int value = step * (ticks - i);
            float y = plot.Top + plot.Height * i / ticks;
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            string lbl = value.ToString();
            SizeF sz = g.MeasureString(lbl, axisFont);
            g.DrawString(lbl, axisFont, axisBrush, plot.Left - sz.Width - 4, y - sz.Height / 2);
        }

        // ---- 3 cột dữ liệu ----
        string[] names = { "Tổng", "PASS", "FAIL" };
        int[] values = { _total, _pass, _fail };
        Color[] colors = { TotalColor, PassColor, FailColor };

        float slot = plot.Width / 3f;
        float barW = Math.Max(10, Math.Min(96f * DeviceDpi / 96F, slot * 0.6f));
        using Font valueFont = new("Segoe UI", 14F, FontStyle.Bold);
        using Font labelFont = new("Segoe UI", 11F, FontStyle.Bold);

        for (int i = 0; i < 3; i++)
        {
            float h = (float)values[i] / vmax * plot.Height;
            if (values[i] > 0) h = Math.Max(2f, h);
            float x = plot.Left + slot * i + (slot - barW) / 2;
            float y = plot.Bottom - h;

            using (var brush = new SolidBrush(colors[i]))
            {
                using var path = RoundedBar(new RectangleF(x, y, barW, h), 4f);
                g.FillPath(brush, path);
            }

            // Nhãn số lượng trên đầu cột - giống #VALY của chart cũ.
            string valueText = values[i].ToString();
            SizeF vs = g.MeasureString(valueText, valueFont);
            float vy = y - vs.Height - 2;
            using var valueBrush = new SolidBrush(colors[i]);
            if (vy >= 0)
            {
                g.DrawString(valueText, valueFont, valueBrush,
                    x + (barW - vs.Width) / 2, vy);
            }
            else
            {
                // Cột quá cao sát trần: đặt nhãn bên trong đầu cột, chữ trắng.
                g.DrawString(valueText, valueFont, Brushes.White,
                    x + (barW - vs.Width) / 2, Math.Min(plot.Bottom - vs.Height, y + 2));
            }

            // Nhãn trục X: TOTAL / PASS / FAIL
            SizeF ns = g.MeasureString(names[i], labelFont);
            g.DrawString(names[i], labelFont, valueBrush,
                plot.Left + slot * i + (slot - ns.Width) / 2, plot.Bottom + 5);
        }

        // ---- trục đáy ----
        using var axisPen = new Pen(AxisColor);
        g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
    }

    /// <summary>Chọn bước chia đẹp cho trục số nguyên: 1, 2, 5, 10, 20, 25, 50...</summary>
    private static int NiceStep(double rough)
    {
        int[] nice =
        {
            1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500,
            1000, 2000, 2500, 5000, 10000, 20000, 50000
        };
        foreach (int n in nice) if (n >= rough) return n;
        return (int)Math.Ceiling(rough);
    }

    /// <summary>Thanh chữ nhật bo nhẹ 2 góc trên; cột 0 vẫn vẽ được (chiều cao tối thiểu).</summary>
    private static GraphicsPath RoundedBar(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.1f)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddLine(r.Right, r.Top + d / 2, r.Right, r.Bottom);
        path.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
        path.AddLine(r.Left, r.Bottom, r.Left, r.Top + d / 2);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// BIỂU ĐỒ TRÒN TỶ LỆ ĐẠT - mô phỏng <c>chart2</c> của Hipot_GPT9801:
///   - miếng XANH LÁ = PASS, miếng ĐỎ = FAIL
///   - nhãn % trên từng miếng (giống series.Label = "#PERCENT")
///   - chú giải bên phải hiện số lượng + FPY của ca hiện tại
/// </summary>
public sealed class YieldPieChart : Control
{
    private int _pass;
    private int _fail;
    private double _fpy;

    private static readonly Color PassColor = Color.FromArgb(34, 139, 34);
    private static readonly Color FailColor = Color.FromArgb(200, 38, 38);
    private static readonly Color EmptyColor = Color.FromArgb(226, 232, 240);
    private static readonly Color TextColor = Color.FromArgb(51, 65, 85);

    public YieldPieChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 11F);
        Padding = new Padding(10, 8, 8, 8);
    }

    public void SetData(int pass, int fail, double fpyPercent)
    {
        _pass = Math.Max(0, pass);
        _fail = Math.Max(0, fail);
        _fpy = fpyPercent;
        Invalidate();
    }

    private int Total => _pass + _fail;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Padding p = Padding;
        float scale = DeviceDpi / 96F;
        float legendWidth = 142f * scale;
        float availableW = Width - p.Left - p.Right;
        bool legendBelow = availableW < 300 * scale;
        float availableH = Height - p.Top - p.Bottom;
        float size = legendBelow
            ? Math.Min(availableH - 60 * scale, availableW)
            : Math.Min(availableH, availableW - legendWidth - 16f * scale);
        size = Math.Max(40f, size);

        // Chú giải chuyển xuống dưới khi cột hẹp để giữ bánh tròn đủ lớn.
        float groupWidth = legendBelow ? size : size + 16f * scale + legendWidth;
        float startX = p.Left + Math.Max(0, (availableW - groupWidth) / 2);
        RectangleF pie = new(startX,
            p.Top + (availableH - size - (legendBelow ? 60 * scale : 0)) / 2, size, size);

        using var passBrush = new SolidBrush(PassColor);
        using var failBrush = new SolidBrush(FailColor);
        using var whitePen = new Pen(Color.White, 2f);

        if (Total == 0)
        {
            // Chưa có phép thử: vòng xám + ghi chú.
            using var empty = new SolidBrush(EmptyColor);
            g.FillEllipse(empty, pie);
            using var gray = new SolidBrush(Color.FromArgb(148, 163, 184));
            DrawCentered(g, "Chưa có dữ liệu", pie, gray, 10F);
        }
        else
        {
            float passSweep = (float)(_pass * 360.0 / Total);

            // Vẽ PASS trước, FAIL phủ phần còn lại - cùng thứ tự chart2 bản cũ.
            if (_pass > 0) g.FillPie(passBrush, pie.X, pie.Y, pie.Width, pie.Height, -90f, passSweep);
            if (_fail > 0)
                g.FillPie(failBrush, pie.X, pie.Y, pie.Width, pie.Height,
                          -90f + passSweep, 360f - passSweep);
            g.DrawEllipse(whitePen, pie);

            // Nhãn % trên từng miếng (bỏ qua miếng quá nhỏ để chữ không đè nhau).
            DrawSlicePercent(g, pie, _pass, Total, -90f, passSweep);
            DrawSlicePercent(g, pie, _fail, Total, -90f + passSweep, 360f - passSweep);
        }

        // ---- chú giải: PASS / FAIL / FPY / TOTAL ----
        float lx = legendBelow ? p.Left + 4 * scale : pie.Right + 16 * scale;
        float ly = legendBelow ? pie.Bottom + 8 * scale : pie.Top + Math.Max(0, (size - 112 * scale) / 2);
        using var labelFont = new Font("Segoe UI", 10F);
        using var boldFont = new Font("Segoe UI", 11F, FontStyle.Bold);

        if (legendBelow)
        {
            float secondX = p.Left + availableW / 2;
            g.FillRectangle(passBrush, lx, ly + 4 * scale, 12 * scale, 12 * scale);
            g.DrawString($"PASS {_pass}", boldFont, Brushes.Black, lx + 17 * scale, ly);
            g.FillRectangle(failBrush, secondX, ly + 4 * scale, 12 * scale, 12 * scale);
            g.DrawString($"FAIL {_fail}", boldFont, Brushes.Black, secondX + 17 * scale, ly);
            using var summaryBrush = new SolidBrush(TextColor);
            g.DrawString($"Tỷ lệ {_fpy:0.0}%", labelFont, summaryBrush, lx, ly + 26 * scale);
            g.DrawString($"Tổng {Total}", labelFont, summaryBrush, secondX, ly + 26 * scale);
            return;
        }

        g.FillRectangle(passBrush, lx, ly + 2 * scale, 12 * scale, 12 * scale);
        g.DrawString($"PASS  {_pass}", boldFont, Brushes.Black, lx + 17 * scale, ly);
        g.FillRectangle(failBrush, lx, ly + 34 * scale, 12 * scale, 12 * scale);
        g.DrawString($"FAIL  {_fail}", boldFont, Brushes.Black, lx + 17 * scale, ly + 32 * scale);

        using var textBrush = new SolidBrush(TextColor);
        g.DrawString($"Tỷ lệ đạt {_fpy:0.0}%", labelFont, textBrush, lx, ly + 68 * scale);
        g.DrawString($"Tổng {Total}", labelFont, textBrush, lx, ly + 94 * scale);
    }

    private static void DrawSlicePercent(Graphics g, RectangleF pie, int value, int total,
                                         float start, float sweep)
    {
        if (total <= 0 || value <= 0 || sweep < 24f) return;

        double mid = (start + sweep / 2) * Math.PI / 180.0;
        float cx = pie.Left + pie.Width / 2f + (float)(Math.Cos(mid) * pie.Width * 0.32);
        float cy = pie.Top + pie.Height / 2f + (float)(Math.Sin(mid) * pie.Height * 0.32);
        string text = $"{value * 100.0 / total:0.#}%";

        using Font font = new("Segoe UI", 12F, FontStyle.Bold);
        SizeF sz = g.MeasureString(text, font);
        using var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
        using var white = new SolidBrush(Color.White);
        g.DrawString(text, font, shadow, cx - sz.Width / 2 + 1, cy - sz.Height / 2 + 1);
        g.DrawString(text, font, white, cx - sz.Width / 2, cy - sz.Height / 2);
    }

    private static void DrawCentered(Graphics g, string text, RectangleF area, Brush brush, float fontSize)
    {
        using Font f = new("Segoe UI", fontSize, FontStyle.Bold);
        SizeF sz = g.MeasureString(text, f);
        g.DrawString(text, f, brush, area.Left + (area.Width - sz.Width) / 2,
                                  area.Top + (area.Height - sz.Height) / 2);
    }
}

/// <summary>
/// BIỂU ĐỒ CỘT XU HƯỚNG DÒNG RÒ (mA) - mỗi cột = 1 phép thử của ca hiện tại:
///   - cột XANH LÁ = PASS, cột ĐỎ = FAIL, ĐỎ TỐI = ERROR (vẫn tính FAIL)
///   - nét đứt đỏ ngang = ngưỡng Hi set (mA) của chương trình đang chạy
///   - nét đứt xanh = ngưỡng Low set &gt; 0 (nếu chương trình có đặt)
///   - nhãn mA trên đầu cột FAIL/ERROR để nhìn ngay điểm lỗi
///   - trục X hiển thị số thứ tự phép thử (cột No trong báo cáo CSV)
/// Nguồn dữ liệu: <c>TestStats.Points</c> - tối đa <c>TestStats.MaxPoints</c> (60)
/// phép thử GẦN NHẤT, nạp lại từ báo cáo CSV khi mở app giữa ca
/// (<c>ReportWriter.ReadRecentResults</c>) nên biểu đồ không bắt đầu từ trắng.
/// Vẽ tay bằng GDI+ ở sự kiện Paint - không cần thư viện ngoài (tương thích .NET 8).
/// </summary>
public sealed class CurrentTrendChart : Control
{
    private IReadOnlyList<TestResultPoint> _points = Array.Empty<TestResultPoint>();
    private double _hiSetMa;
    private double _lowSetMa;

    private static readonly Color PassColor = Color.FromArgb(34, 139, 34);
    private static readonly Color FailColor = Color.FromArgb(200, 38, 38);
    private static readonly Color ErrorColor = Color.FromArgb(120, 20, 20);
    private static readonly Color LowColor = Color.FromArgb(37, 99, 235);
    private static readonly Color GridColor = Color.FromArgb(226, 232, 240);
    private static readonly Color AxisColor = Color.FromArgb(100, 116, 139);
    private static readonly Color EmptyColor = Color.FromArgb(148, 163, 184);

    public CurrentTrendChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 11F);
        Padding = new Padding(64, 36, 16, 48);
    }

    /// <summary>Số cột hiện có (phép thử gần nhất) - dùng cho test headless.</summary>
    public int PointCount => _points.Count;

    /// <summary>Cập nhật dữ liệu: điểm xu hướng + ngưỡng Hi/Low set (mA) của chương trình.</summary>
    public void SetData(IReadOnlyList<TestResultPoint>? points, double hiSetMa, double lowSetMa = 0)
    {
        _points = points ?? Array.Empty<TestResultPoint>();
        _hiSetMa = Math.Max(0, hiSetMa);
        _lowSetMa = Math.Max(0, lowSetMa);
        Invalidate();
    }

    /// <summary>
    /// Chọn scale trục Y (mA): 5 khoảng đều, làm tròn lên bước đẹp
    /// (0.001, 0.002, 0.005, 0.01 ... 5, 10, 20, 25, 50) và luôn phủ hết
    /// giá trị lớn nhất cũng như ngưỡng Hi set (để nét đứt luôn nằm trong lưới).
    /// Nếu dữ liệu chạm &gt; 90% đỉnh thì thêm 1 khoảng cho nhãn số trên cột.
    /// </summary>
    public static (double VMax, double Step) ComputeScale(double maxValue, double hiLimit)
    {
        double max = Math.Max(Math.Max(0, maxValue), Math.Max(0, hiLimit));
        if (max <= 0) max = 1;
        double step = NiceStep(max / 5.0);
        double vmax = step * 5;
        if (max > vmax * 0.9) vmax += step;
        return (vmax, step);
    }

    /// <summary>Bước chia đẹp cho trục mA (hỗ trợ cả giá trị nhỏ hơn 1).</summary>
    private static double NiceStep(double rough)
    {
        double[] nice =
        {
            0.001, 0.002, 0.005, 0.01, 0.02, 0.05,
            0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 25, 50, 100
        };
        foreach (double n in nice) if (n >= rough) return n;
        return Math.Ceiling(rough);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float plotTop = Math.Max(Padding.Top, DrawLegend(g) + 8 * DeviceDpi / 96F);
        RectangleF plot = new(Padding.Left, plotTop,
            Math.Max(10, Width - Padding.Left - Padding.Right),
            Math.Max(10, Height - plotTop - Padding.Bottom));

        if (_points.Count == 0)
        {
            using var gray = new SolidBrush(EmptyColor);
            using Font f = new("Segoe UI", 11F, FontStyle.Bold);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
            };
            g.DrawString("Chưa có dữ liệu dòng rò\nTự nạp báo cáo của ca hiện tại", f, gray, plot, format);
            return;
        }

        // ---- scale dọc từ dữ liệu + ngưỡng Hi/Low ----
        double maxData = _points.Max(p => p.CurrentMa);
        var (vmax, step) = ComputeScale(maxData, Math.Max(_hiSetMa, _lowSetMa));
        int ticks = (int)Math.Round(vmax / step);

        using Font axisFont = new("Consolas", 9.5F);
        using var axisBrush = new SolidBrush(AxisColor);
        using var gridPen = new Pen(GridColor);

        // ---- lưới ngang + nhãn trục Y (mA) ----
        for (int i = 0; i <= ticks; i++)
        {
            double value = step * (ticks - i);
            float y = plot.Top + plot.Height * i / ticks;
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            string lbl = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            SizeF sz = g.MeasureString(lbl, axisFont);
            g.DrawString(lbl, axisFont, axisBrush, plot.Left - sz.Width - 4, y - sz.Height / 2);
        }
        g.DrawString("mA", axisFont, axisBrush, 4, plot.Top - 14);

        // ---- 1 cột cho mỗi phép thử gần nhất ----
        int n = _points.Count;
        float slot = plot.Width / n;
        float barW = Math.Max(1.5f, Math.Min(36f, slot * 0.68f));
        using Font valueFont = new("Consolas", 9F, FontStyle.Bold);
        using Font labelFont = new("Consolas", 9F);
        int labelEvery = Math.Max(1, (int)Math.Ceiling(n / Math.Max(1, plot.Width / (48 * DeviceDpi / 96F))));

        for (int i = 0; i < n; i++)
        {
            TestResultPoint p = _points[i];
            float h = (float)(p.CurrentMa / vmax * plot.Height);
            if (p.CurrentMa > 0) h = Math.Max(1.5f, h);
            float x = plot.Left + slot * i + (slot - barW) / 2;
            float y = plot.Bottom - h;

            Color color = p.Result switch
            {
                "PASS" => PassColor,
                "ERROR" => ErrorColor,
                _ => FailColor
            };
            using (var brush = new SolidBrush(color))
                g.FillRectangle(brush, x, y, barW, h);

            // Nhãn mA trên đầu cột lỗi - nhìn ngay điểm dòng rò vượt ngưỡng.
            if (p.Result is not ("PASS"))
            {
                string txt = p.CurrentMa.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                SizeF vs = g.MeasureString(txt, valueFont);
                if (vs.Width <= slot)
                {
                    using var vb = new SolidBrush(color);
                    g.DrawString(txt, valueFont, vb, x + (barW - vs.Width) / 2,
                        Math.Max(0, y - vs.Height - 1));
                }
            }

            // Nhãn trục X: số thứ tự phép thử, giãn để không đè nhau.
            if (i % labelEvery == 0)
            {
                string no = p.Index.ToString();
                SizeF ns = g.MeasureString(no, labelFont);
                g.DrawString(no, labelFont, axisBrush,
                    plot.Left + slot * i + (slot - ns.Width) / 2, plot.Bottom + 4);
            }
        }

        // ---- ngưỡng Low (nếu có) + ngưỡng Hi set: nét đứt ngang ----
        using (var hiPen = new Pen(FailColor, 1.4f) { DashStyle = DashStyle.Dash })
        {
            float hy = (float)(_hiSetMa / vmax * plot.Height);
            float yLine = plot.Bottom - hy;
            g.DrawLine(hiPen, plot.Left, yLine, plot.Right, yLine);
            string hiTxt = $"Trên {_hiSetMa:0.###} mA";
            using var hiFont = new Font("Consolas", 9F, FontStyle.Bold);
            SizeF hs = g.MeasureString(hiTxt, hiFont);
            using var hiBrush = new SolidBrush(FailColor);
            g.DrawString(hiTxt, hiFont, hiBrush,
                plot.Right - hs.Width - 2, Math.Max(0, yLine - hs.Height - 2));
        }
        if (_lowSetMa > 0)
        {
            using var lowPen = new Pen(LowColor, 1.4f) { DashStyle = DashStyle.Dash };
            float ly = plot.Bottom - (float)(_lowSetMa / vmax * plot.Height);
            g.DrawLine(lowPen, plot.Left, ly, plot.Right, ly);
            string lowTxt = $"Dưới {_lowSetMa:0.###} mA";
            using var lowFont = new Font("Consolas", 9F);
            SizeF ls = g.MeasureString(lowTxt, lowFont);
            using var lowBrush = new SolidBrush(LowColor);
            g.DrawString(lowTxt, lowFont, lowBrush, plot.Right - ls.Width - 2, ly + 2);
        }

        // ---- trục đáy + nhãn trục X ----
        using var axisPen = new Pen(AxisColor);
        g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        const string axisTitle = "Số thứ tự phép thử";
        SizeF titleSize = g.MeasureString(axisTitle, labelFont);
        g.DrawString(axisTitle, labelFont, axisBrush,
            plot.Left + (plot.Width - titleSize.Width) / 2, plot.Bottom + 24);
    }

    /// <summary>Chú giải nhỏ phía trên: ô xanh PASS, ô đỏ FAIL, nét đứt Hi set.</summary>
    private float DrawLegend(Graphics g)
    {
        using Font f = new("Segoe UI", 9F);
        using var text = new SolidBrush(AxisColor);
        float scale = DeviceDpi / 96F;
        var entries = new[] { ("PASS", PassColor), ("FAIL", FailColor), ("Lỗi", ErrorColor), ("Ngưỡng trên", FailColor) };
        float marker = 14 * scale;
        float gap = 10 * scale;
        float totalWidth = entries.Sum(entry => g.MeasureString(entry.Item1, f).Width + marker + gap);
        float x = Math.Max(4 * scale, Width - Padding.Right - totalWidth);
        float y = 5 * scale;
        float lineHeight = f.GetHeight(g) + 5 * scale;
        for (int i = 0; i < entries.Length; i++)
        {
            var (label, color) = entries[i];
            float itemWidth = g.MeasureString(label, f).Width + marker + gap;
            if (x > 4 * scale && x + itemWidth > Width - Padding.Right)
            {
                x = 4 * scale;
                y += lineHeight;
            }
            if (i == 3)
            {
                using var pen = new Pen(color, 1.4f) { DashStyle = DashStyle.Dash };
                g.DrawLine(pen, x, y + 7 * scale, x + 11 * scale, y + 7 * scale);
            }
            else
            {
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, x, y + 2 * scale, 10 * scale, 10 * scale);
            }
            g.DrawString(label, f, text, x + marker, y);
            x += itemWidth;
        }
        return y + lineHeight;
    }
}
