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
        Font = new Font("Segoe UI", 8F);
        Padding = new Padding(42, 18, 10, 26);
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

        using Font axisFont = new("Consolas", 7.5F);
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
        string[] names = { "TOTAL", "PASS", "FAIL" };
        int[] values = { _total, _pass, _fail };
        Color[] colors = { TotalColor, PassColor, FailColor };

        float slot = plot.Width / 3f;
        float barW = Math.Max(10, Math.Min(72f, slot * 0.52f));
        using Font valueFont = new("Segoe UI", 9.5F, FontStyle.Bold);
        using Font labelFont = new("Segoe UI", 8F, FontStyle.Bold);

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
        Font = new Font("Segoe UI", 8F);
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
        const float legendWidth = 118f;
        float availableW = Width - p.Left - p.Right;
        float size = Math.Min(Height - p.Top - p.Bottom,
                              availableW - legendWidth - 16f);
        size = Math.Max(40f, size);

        // Căn giữa cả cụm "bánh tròn + chú giải" trong cột 40% (tránh lệch trái).
        float startX = p.Left + Math.Max(0, (availableW - (size + 16f + legendWidth)) / 2);
        RectangleF pie = new(startX,
            p.Top + ((Height - p.Top - p.Bottom) - size) / 2, size, size);

        using var passBrush = new SolidBrush(PassColor);
        using var failBrush = new SolidBrush(FailColor);
        using var whitePen = new Pen(Color.White, 2f);

        if (Total == 0)
        {
            // Chưa có phép thử: vòng xám + ghi chú.
            using var empty = new SolidBrush(EmptyColor);
            g.FillEllipse(empty, pie);
            using var gray = new SolidBrush(Color.FromArgb(148, 163, 184));
            DrawCentered(g, "Chưa có dữ liệu", pie, gray, 8F);
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
        float lx = pie.Right + 16;
        float ly = pie.Top + Math.Max(0, (size - 96) / 2);
        using var labelFont = new Font("Segoe UI", 8F);
        using var boldFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);

        g.FillRectangle(passBrush, lx, ly + 2, 12, 12);
        g.DrawString($"PASS  {_pass}", boldFont, Brushes.Black, lx + 17, ly);
        g.FillRectangle(failBrush, lx, ly + 28, 12, 12);
        g.DrawString($"FAIL  {_fail}", boldFont, Brushes.Black, lx + 17, ly + 26);

        using var textBrush = new SolidBrush(TextColor);
        g.DrawString($"FPY   {_fpy:0.0}%", labelFont, textBrush, lx, ly + 56);
        g.DrawString($"TOTAL {Total}", labelFont, textBrush, lx, ly + 76);
    }

    private static void DrawSlicePercent(Graphics g, RectangleF pie, int value, int total,
                                         float start, float sweep)
    {
        if (total <= 0 || value <= 0 || sweep < 24f) return;

        double mid = (start + sweep / 2) * Math.PI / 180.0;
        float cx = pie.Left + pie.Width / 2f + (float)(Math.Cos(mid) * pie.Width * 0.32);
        float cy = pie.Top + pie.Height / 2f + (float)(Math.Sin(mid) * pie.Height * 0.32);
        string text = $"{value * 100.0 / total:0.#}%";

        using Font font = new("Segoe UI", 9F, FontStyle.Bold);
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
