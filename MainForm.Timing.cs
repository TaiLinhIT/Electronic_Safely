namespace GPT9000_WinForms_TestSystem;

public partial class MainForm
{
    private readonly GroupBox _grpFixtureTiming = new();
    private readonly GroupBox _grpPcTiming = new();
    private readonly Label _timingStatus = new();
    private readonly Dictionary<string, NumericUpDown> _timingInputs = new();
    private bool _fixtureTimingReady;
    private bool _fixtureStartPending;
    private bool _returningToIdle;

    private void BuildTimingSettings()
    {
        TableLayoutPanel Group(GroupBox group, string title)
        {
            group.Text = title;
            group.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            group.Padding = new Padding(12, 24, 12, 8);
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            group.Controls.Add(table);
            return table;
        }
        void Input(TableLayoutPanel table, int row, string name, string label, int min, int max)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var caption = new Label
            {
                Text = label + " (giây)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 8, 0)
            };
            var input = new NumericUpDown
            {
                Name = name, AccessibleName = label, DecimalPlaces = 3, Increment = 0.1M,
                Minimum = min / 1000M, Maximum = max / 1000M, Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F), TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(0, 7, 0, 3), ThousandsSeparator = false
            };
            table.Controls.Add(caption, 0, row);
            table.Controls.Add(input, 1, row);
            _timingInputs.Add(name, input);
        }
        void Note(TableLayoutPanel table, int row, string text, int height)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var label = new Label
            {
                Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(0, 8, 0, 0)
            };
            table.Controls.Add(label, 0, row);
            table.SetColumnSpan(label, 2);
        }
        var board = Group(_grpFixtureTiming, "THỜI GIAN ĐỒ GÁ VÀ KIM TIẾP XÚC");
        Input(board, 0, nameof(CycleTimingSettings.FixtureCloseMs), "Đóng đồ gá trước khi đẩy kim", 100, 7500);
        Input(board, 1, nameof(CycleTimingSettings.PogoOutMs), "Đẩy kim trước khi cho phép đo", 100, 7500);
        Input(board, 2, nameof(CycleTimingSettings.PogoBackMs), "Rút kim trước khi mở đồ gá", 100, 7500);
        Input(board, 3, nameof(CycleTimingSettings.FixtureOpenMs), "Mở đồ gá trước khi sẵn sàng", 100, 7500);
        Input(board, 4, nameof(CycleTimingSettings.ArmWaitMs), "Chờ máy thử xác nhận sẵn sàng", 1000, 30000);
        Note(board, 5, "Đóng/mở, đẩy/rút: 0,1–7,5 giây. Chờ xác nhận: 1–30 giây. " +
            "Thời gian được gửi xuống bảng điều khiển khi kết nối hoặc lưu cấu hình.", 64);
        board.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _timingStatus.Dock = DockStyle.Fill;
        _timingStatus.Font = new Font("Segoe UI", 9F);
        _timingStatus.Text = "Chưa kết nối bảng điều khiển để xác nhận thời gian.";
        board.Controls.Add(_timingStatus, 0, 6);
        board.SetColumnSpan(_timingStatus, 2);

        var pc = Group(_grpPcTiming, "THỜI GIAN CHỜ CỦA CHƯƠNG TRÌNH");
        Input(pc, 0, nameof(CycleTimingSettings.MeasurementReadyDelayMs), "Chờ thêm sau thử và tăng áp", 0, 10000);
        Input(pc, 1, nameof(CycleTimingSettings.TestCompletionMarginMs), "Dự phòng chờ máy thử hoàn tất", 1000, 60000);
        Input(pc, 2, nameof(CycleTimingSettings.MeasurementReadTimeoutMs), "Chờ phản hồi kết quả đo", 500, 30000);
        Input(pc, 3, nameof(CycleTimingSettings.ErrorHoldMs), "Tạm giữ thông báo lỗi khởi động", 0, 10000);
        Input(pc, 4, nameof(CycleTimingSettings.PostCycleDelayMs), "Chờ sau khi kết thúc phép thử", 0, 10000);
        Input(pc, 5, nameof(CycleTimingSettings.FixtureReturnTimeoutMs), "Chờ tối đa đồ gá về vị trí nghỉ", 1000, 30000);
        Note(pc, 6, "Chờ đồ gá về phải lớn hơn tổng thời gian rút kim và mở đồ gá ít nhất 0,5 giây. " +
            "Thời gian thử và tăng áp vẫn cài riêng trong chương trình phép thử.", 80);
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
        foreach (int height in new[] { 112, 112, 410, 126 })
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
        Add(_grpFixtureTiming, 0, 2);
        Add(_grpPcTiming, 1, 2);
        Add(_grpAction, 0, 3, 2);
        Add(_grpOfficeLicense, 0, 4, 2);
        _grpHipot.Text = "MÁY THỬ CAO ÁP - GPT-9801 (RS-232)";
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
            (nameof(t.PogoBackMs), t.PogoBackMs), (nameof(t.FixtureOpenMs), t.FixtureOpenMs),
            (nameof(t.ArmWaitMs), t.ArmWaitMs), (nameof(t.MeasurementReadyDelayMs), t.MeasurementReadyDelayMs),
            (nameof(t.TestCompletionMarginMs), t.TestCompletionMarginMs), (nameof(t.MeasurementReadTimeoutMs), t.MeasurementReadTimeoutMs),
            (nameof(t.ErrorHoldMs), t.ErrorHoldMs), (nameof(t.PostCycleDelayMs), t.PostCycleDelayMs),
            (nameof(t.FixtureReturnTimeoutMs), t.FixtureReturnTimeoutMs)
        }) _timingInputs[name].Value = ms / 1000M;
    }

    private CycleTimingSettings ReadTimingInputs()
    {
        int Ms(string name) => decimal.ToInt32(_timingInputs[name].Value * 1000M);
        return new()
        {
            FixtureCloseMs = Ms(nameof(CycleTimingSettings.FixtureCloseMs)),
            PogoOutMs = Ms(nameof(CycleTimingSettings.PogoOutMs)),
            PogoBackMs = Ms(nameof(CycleTimingSettings.PogoBackMs)),
            FixtureOpenMs = Ms(nameof(CycleTimingSettings.FixtureOpenMs)),
            ArmWaitMs = Ms(nameof(CycleTimingSettings.ArmWaitMs)),
            MeasurementReadyDelayMs = Ms(nameof(CycleTimingSettings.MeasurementReadyDelayMs)),
            TestCompletionMarginMs = Ms(nameof(CycleTimingSettings.TestCompletionMarginMs)),
            MeasurementReadTimeoutMs = Ms(nameof(CycleTimingSettings.MeasurementReadTimeoutMs)),
            ErrorHoldMs = Ms(nameof(CycleTimingSettings.ErrorHoldMs)),
            PostCycleDelayMs = Ms(nameof(CycleTimingSettings.PostCycleDelayMs)),
            FixtureReturnTimeoutMs = Ms(nameof(CycleTimingSettings.FixtureReturnTimeoutMs))
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
