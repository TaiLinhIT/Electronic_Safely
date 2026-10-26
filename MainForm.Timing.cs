namespace GPT9000_WinForms_TestSystem;

public partial class MainForm
{
    private readonly GroupBox _grpFixtureTiming = new();
    private readonly Label _timingStatus = new();
    private readonly Dictionary<string, NumericUpDown> _timingInputs = new();
    private bool _fixtureTimingReady;
    private bool _fixtureStartPending;
    private bool _returningToIdle;

    private void BuildTimingSettings()
    {
        _grpFixtureTiming.Text = "THỜI GIAN ĐỒ GÁ VÀ KIM POGO (GIÂY)";
        _grpFixtureTiming.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _grpFixtureTiming.Padding = new Padding(12, 24, 12, 8);
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foreach (int height in new[] { 60, 60, 24, 40 })
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _grpFixtureTiming.Controls.Add(table);

        void Input(int column, int row, string name, string title, string detail)
        {
            var field = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.White, Padding = new Padding(8), Margin = new Padding(4)
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var caption = new Label
            {
                Text = title + "\r\n" + detail, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 8, 0)
            };
            var input = new NumericUpDown
            {
                Name = name, AccessibleName = title, DecimalPlaces = 3, Increment = 0.1M,
                Minimum = 0.1M, Maximum = 7.5M, Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(0, 6, 0, 6), ThousandsSeparator = false
            };
            field.Controls.Add(caption, 0, 0);
            field.Controls.Add(input, 1, 0);
            table.Controls.Add(field, column, row);
            _timingInputs.Add(name, input);
        }
        Input(0, 0, nameof(CycleTimingSettings.FixtureCloseMs), "1. Đóng đồ gá", "Chờ trước khi đẩy kim pogo");
        Input(1, 0, nameof(CycleTimingSettings.PogoOutMs), "2. Đẩy kim pogo", "Chờ trước khi bắt đầu đo");
        Input(0, 1, nameof(CycleTimingSettings.PogoBackMs), "3. Rút kim pogo", "Chờ trước khi mở đồ gá");
        Input(1, 1, nameof(CycleTimingSettings.FixtureOpenMs), "4. Mở đồ gá", "Chờ trước sản phẩm tiếp theo");
        var note = new Label
        {
            Text = "Mỗi thời gian từ 0,1 đến 7,5 giây.", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(4, 0, 4, 0)
        };
        table.Controls.Add(note, 0, 2);
        table.SetColumnSpan(note, 2);
        _timingStatus.Dock = DockStyle.Fill;
        _timingStatus.Font = new Font("Segoe UI", 9F);
        _timingStatus.Text = "Chưa kết nối bảng điều khiển để xác nhận thời gian.";
        _timingStatus.Margin = new Padding(4, 0, 4, 0);
        table.Controls.Add(_timingStatus, 0, 3);
        table.SetColumnSpan(_timingStatus, 2);
    }

    private void ArrangeSettingsLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 5,
            Padding = new Padding(8), MinimumSize = new Size(1000, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foreach (int height in new[] { 100, 92, 228, 126 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grpOfficeLicense.MinimumSize = new Size(0, (int)(135 * DeviceDpi / 96F));
        void Add(Control control, int column, int row, int span = 1)
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(6);
            layout.Controls.Add(control, column, row);
            layout.SetColumnSpan(control, span);
        }
        Add(_grpHipot, 0, 0);
        Add(_grpCb, 1, 0);
        Add(_grpSecurity, 0, 1, 2);
        Add(_grpFixtureTiming, 0, 2, 2);
        Add(_grpAction, 0, 3, 2);
        Add(_grpOfficeLicense, 0, 4, 2);
        _grpHipot.Text = "MÁY THỬ CAO ÁP - GPT-9801 (USB / RS-232)";
        _lblSetStatus.Location = new Point(20, 80);
        _lblSetStatus.Size = new Size(900, 26);
        _lblSetStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        ScaleDashboard(layout, DeviceDpi / 96F);
        _tabSettings.Controls.Add(layout);
    }

    private void LoadTimingInputs()
    {
        var t = _setting.Timing;
        foreach (var (name, ms) in new[]
        {
            (nameof(t.FixtureCloseMs), t.FixtureCloseMs), (nameof(t.PogoOutMs), t.PogoOutMs),
            (nameof(t.PogoBackMs), t.PogoBackMs), (nameof(t.FixtureOpenMs), t.FixtureOpenMs)
        }) _timingInputs[name].Value = ms / 1000M;
    }

    private CycleTimingSettings ReadTimingInputs()
    {
        int Ms(string name) => decimal.ToInt32(_timingInputs[name].Value * 1000M);
        var previous = _setting.Timing;
        int pogoBack = Ms(nameof(CycleTimingSettings.PogoBackMs));
        int fixtureOpen = Ms(nameof(CycleTimingSettings.FixtureOpenMs));
        return new()
        {
            FixtureCloseMs = Ms(nameof(CycleTimingSettings.FixtureCloseMs)),
            PogoOutMs = Ms(nameof(CycleTimingSettings.PogoOutMs)),
            PogoBackMs = pogoBack,
            FixtureOpenMs = fixtureOpen,
            ArmWaitMs = previous.ArmWaitMs,
            MeasurementReadyDelayMs = previous.MeasurementReadyDelayMs,
            TestCompletionMarginMs = previous.TestCompletionMarginMs,
            MeasurementReadTimeoutMs = previous.MeasurementReadTimeoutMs,
            ErrorHoldMs = previous.ErrorHoldMs,
            PostCycleDelayMs = previous.PostCycleDelayMs,
            FixtureReturnTimeoutMs = Math.Max(previous.FixtureReturnTimeoutMs, pogoBack + fixtureOpen + 500)
        };
    }

    private FixtureTimingResult ApplyBoardTimings(CycleTimingSettings timing)
    {
        var result = FixtureTimingProtocol.Apply(timing, command => _arduino.SendCommand(command, 1500));
        _fixtureTimingReady = result.Ready;
        _timingStatus.Text = result.Message;
        _timingStatus.ForeColor = result.Applied ? Color.ForestGreen : Color.DarkOrange;
        AddLog("[CT] " + result.Message);
        return result;
    }

    private bool SettingsError(string message, bool silent)
    {
        _lblSetStatus.Text = message;
        _lblSetStatus.ForeColor = Color.DarkRed;
        if (!silent) VietnameseMessageBox.Show(this, message, "Chưa lưu cấu hình",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
}
