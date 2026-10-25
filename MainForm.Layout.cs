namespace GPT9000_WinForms_TestSystem;

public partial class MainForm
{
    private readonly PictureBox _headerLogo = new();
    private bool _fittingLabelFont;

    private void StyleHeader()
    {
        AppBranding.ApplyIcon(this);
        var assembly = typeof(MainForm).Assembly;
        using (var logoStream = assembly.GetManifestResourceStream("Branding.Logo.png")!)
        using (var source = Image.FromStream(logoStream))
            _headerLogo.Image = new Bitmap(source);

        _headerPanel.BackColor = Color.FromArgb(30, 58, 95);
        _headerPanel.Height = (int)(90 * DeviceDpi / 96F);
        _headerPanel.Padding = new Padding(12, 8, 12, 8);
        _tabs.Dock = DockStyle.Fill;
        _tabs.BringToFront();

        // Hai cột ngoài bằng nhau để tên luôn ở chính giữa cửa sổ.
        var header = CreateTable(3, 1);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _lblTitle.Text = Text;
        _lblTitle.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        _lblTitle.ForeColor = Color.White;
        _lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        _lblTitle.AutoEllipsis = true;
        _headerLogo.SizeMode = PictureBoxSizeMode.Zoom;
        _headerLogo.AccessibleName = "Logo MiT TECH";
        _lblClock.ForeColor = Color.White;
        _lblClock.BackColor = Color.Transparent;
        _lblClock.BorderStyle = BorderStyle.None;
        _lblClock.TextAlign = ContentAlignment.MiddleLeft;
        AddCell(header, _lblClock, 0, 0);
        AddCell(header, _lblTitle, 1, 0);
        AddCell(header, _headerLogo, 2, 0);
        _headerPanel.Controls.Add(header);
        ScaleDashboard(_headerPanel, DeviceDpi / 96F);
        _lblTitle.SizeChanged += (_, _) => FitLabelFont(_lblTitle, 20);
    }

    private void BuildHomeLayout()
    {
        _homeLayout.SuspendLayout();
        _homeLayout.RowStyles[0] = new RowStyle(SizeType.Percent, 52);
        _homeLayout.RowStyles[1] = new RowStyle(SizeType.Percent, 48);
        _homeLayout.RowStyles[2] = new RowStyle(SizeType.Absolute, 116);
        _tabHome.ClientSizeChanged += (_, _) => ApplyHomeColumnSplit();

        BuildInfoLayout();
        BuildSection(_grpTestItems, _lblTestItems, _gridPanel, 32);
        BuildSection(_grpCharts, _lblCharts, _chartsLayout, 32);
        BuildSection(_grpTestLog, _lblTestLog, _logPanel, 28);
        _lblCharts.Text = "BIỂU ĐỒ KẾT QUẢ | TỶ LỆ ĐẠT | DÒNG RÒ (mA)";
        _lblCharts.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _chartsLayout.ColumnStyles[0].Width = 29;
        _chartsLayout.ColumnStyles[1].Width = 31;
        _chartsLayout.ColumnStyles[2].Width = 40;

        var result = CreateTable(1, 3);
        result.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        result.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        result.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        AddCell(result, _lblTestResult, 0, 0);
        AddCell(result, _status, 0, 1);
        AddCell(result, _lblStartHint, 0, 2);
        _grpTestResult.Padding = new Padding(8, 4, 8, 6);
        _grpTestResult.Controls.Add(result);
        ScaleDashboard(_homeLayout, DeviceDpi / 96F);
        _homeLayout.ResumeLayout(true);
        ApplyHomeColumnSplit();
    }

    private void BuildInfoLayout()
    {
        _grpTestInfo.Padding = new Padding(8, 4, 8, 6);
        var info = CreateTable(1, 7);
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int height in new[] { 30, 40, 42, 88, 30, 30 })
            info.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddCell(info, _valProgram, 0, 0);

        var devices = CreateTable(2, 2);
        devices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        devices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        devices.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        devices.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddCell(devices, _lblHipot, 0, 0);
        AddCell(devices, _lblCb, 1, 0);
        AddCell(devices, _valHipot, 0, 1);
        AddCell(devices, _valCb, 1, 1);
        AddCell(info, devices, 0, 1);

        var stats = CreateTable(4, 2);
        stats.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Label[] names = { _lblPass, _lblFail, _lblFpy, _lblTotal };
        Label[] values = { _valPass, _valFail, _valFpy, _valTotal };
        for (int i = 0; i < names.Length; i++)
        {
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            names[i].TextAlign = values[i].TextAlign = ContentAlignment.MiddleCenter;
            values[i].Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            AddCell(stats, names[i], i, 0);
            AddCell(stats, values[i], i, 1);
        }
        AddCell(info, stats, 0, 2);

        var details = CreateTable(2, 4);
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Label[] captions = { _lblCycleTime, _lblModel, _lblApplication, _lblVersion };
        Label[] data = { _valCycleTime, _valModel, _valApplication, _valVersion };
        for (int i = 0; i < captions.Length; i++)
        {
            details.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 2 ? 28 : 20));
            captions[i].TextAlign = ContentAlignment.MiddleLeft;
            data[i].TextAlign = ContentAlignment.MiddleRight;
            data[i].AutoEllipsis = true;
            AddCell(details, captions[i], 0, i);
            AddCell(details, data[i], 1, i);
        }
        AddCell(info, details, 0, 3);
        AddCell(info, _btnSelectProgram, 0, 4);
        var actions = CreateTable(2, 1);
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddCell(actions, _btnOpenLog, 0, 0);
        AddCell(actions, _btnReconnect, 1, 0);
        AddCell(info, actions, 0, 5);
        _grpTestInfo.Controls.Add(info);
    }

    private static TableLayoutPanel CreateTable(int columns, int rows) => new()
    {
        ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill,
        Margin = Padding.Empty, Padding = Padding.Empty
    };

    private static void AddCell(TableLayoutPanel table, Control control, int column, int row)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = Padding.Empty;
        if (control is Label label) label.AutoSize = false;
        table.Controls.Add(control, column, row);
    }

    private static void BuildSection(GroupBox group, Label heading, Control content, int headingHeight)
    {
        var section = CreateTable(1, 2);
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        section.RowStyles.Add(new RowStyle(SizeType.Absolute, headingHeight));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.TextAlign = ContentAlignment.MiddleLeft;
        AddCell(section, heading, 0, 0);
        AddCell(section, content, 0, 1);
        group.Padding = new Padding(8, 4, 8, 6);
        group.Controls.Add(section);
    }

    private void FitStatusFont()
        => FitLabelFont(_status, 40);

    private static void ScaleDashboard(Control root, float scale)
    {
        // Các bảng được tạo sau InitializeComponent nên cần áp dụng DPI một lần.
        if (Math.Abs(scale - 1F) < 0.01F) return;
        root.Padding = new Padding((int)(root.Padding.Left * scale), (int)(root.Padding.Top * scale),
            (int)(root.Padding.Right * scale), (int)(root.Padding.Bottom * scale));
        if (root is TableLayoutPanel table)
        {
            foreach (ColumnStyle column in table.ColumnStyles)
                if (column.SizeType == SizeType.Absolute) column.Width *= scale;
            foreach (RowStyle row in table.RowStyles)
                if (row.SizeType == SizeType.Absolute) row.Height *= scale;
        }
        foreach (Control child in root.Controls) ScaleDashboard(child, scale);
    }

    private void FitLabelFont(Label label, float maximumSize)
    {
        if (_fittingLabelFont || label.ClientSize.Width <= 0 || label.ClientSize.Height <= 0) return;
        _fittingLabelFont = true;
        try
        {
        // Giữ cả tên ứng dụng và trạng thái dài trong vùng hiển thị khi thu hẹp.
        for (float size = maximumSize; size >= 10; size -= 1)
        {
            var font = new Font("Segoe UI", size, FontStyle.Bold);
            Size text = TextRenderer.MeasureText(label.Text, font);
            if (text.Width <= label.ClientSize.Width - 16 &&
                text.Height <= label.ClientSize.Height - 16)
            {
                if (Math.Abs(label.Font.Size - size) < 0.01F)
                {
                    font.Dispose();
                    return;
                }
                Font previous = label.Font;
                Rectangle bounds = label.Bounds;
                label.Font = font;
                // Label có thể khôi phục kích thước thiết kế khi đổi font; giữ ô căn giữa.
                label.Bounds = bounds;
                previous.Dispose();
                return;
            }
            font.Dispose();
        }
        }
        finally
        {
            _fittingLabelFont = false;
        }
    }
}
