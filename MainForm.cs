using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace GPT9000_WinForms_TestSystem;

public partial class MainForm : Form
{
    private static readonly Color ColorIdle = Color.FromArgb(100, 116, 139);
    private static readonly Color ColorTesting = Color.FromArgb(37, 99, 235);
    private static readonly Color ColorPass = Color.ForestGreen;
    private static readonly Color ColorFail = Color.Firebrick;
    private static readonly Color ColorError = Color.DarkRed;
    private static readonly Color ColorOk = Color.ForestGreen;
    private static readonly Color ColorLatestResult = Color.FromArgb(232, 243, 255);

    private readonly IniConfig _config;
    private readonly AppPaths _paths;
    private readonly SystemSetting _setting;
    private readonly ReportWriter _report;
    /// <summary>Ghi log vận hành ra file <c>Log\&lt;yyyy-MM-dd&gt;_Log.csv</c> (nằm trong bin).</summary>
    private readonly LogWriter _logFile;
    private readonly HipotLink _hipot = new();
    private readonly ArduinoLink _arduino = new();
    private readonly DataGridView _grid = new();
    private readonly RichTextBox _log = new();
    private readonly Stopwatch _cycleWatch = new();

    private TestProgram _program = TestProgram.CreateDefault();
    private string _programFile = "program.csv";
    private bool _running;
    private bool _retainTestResult;
    private bool _showingStartError;
    private bool _stopRequested;
    private bool _hipotOk;
    private bool _cbOk;

    /// <summary>Nguồn dữ liệu cho 2 biểu đồ kết quả (TestStats + 2 control ở ResultCharts.cs).</summary>
    private readonly TestStats _stats = new();
    private readonly PassRateBarChart _chartPassRate = new();
    private readonly YieldPieChart _chartPie = new();
    /// <summary>Biểu đồ cột xu hướng dòng rò mA - 60 phép thử gần nhất + ngưỡng Hi/Low set.</summary>
    private readonly CurrentTrendChart _chartTrend = new();

    /// <summary>
    /// true = board đang chạy firmware v1.2.0+ ở chế độ FIXTURE tự động (xilanh khí nén).
    /// Detect bằng dấu hiệu "FIX=" trong phản hồi STATUS?.
    /// Khi bật: nút START (màn hình hoặc vật lý) → board tự đóng xilanh → board báo
    /// *TESTON# → PC mới phát áp. Không còn kiểm nắp an toàn / cảm biến unit ở PC.
    /// </summary>
    private bool _fixtureMode;
    private readonly SettingsAccess _settingsAccess = new();
    private readonly OfficeLicenseService _license = new();
    private bool _licenseAllowsTest;
    private readonly GroupBox _grpOfficeLicense = new();
    private bool _logFileWarningShown;
    private int _pass, _fail, _total;
    private bool _lastInterlock = true;

    /// <summary>Trạng thái xilanh lần cuối poll được - chỉ log khi THAY ĐIỂM để khỏi spam.</summary>
    private string _lastFixState = "";
    private System.Windows.Forms.Timer? _pollTimer;
    private System.Windows.Forms.Timer? _clockTimer;

    public MainForm()
    {
        InitializeComponent();

        // Bước 1 - Khởi động phần mềm: đọc cấu hình và dựng giao diện.
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
        _config = new IniConfig(configPath);
        _paths = new AppPaths();
        _paths.Ensure();
        _setting = SystemSetting.Load(_paths.SettingFile);
        int dayStart = int.TryParse(_config.Get("Shift", "DayStartHour", "8"), out int ds) ? ds : 8;
        int nightStart = int.TryParse(_config.Get("Shift", "NightStartHour", "20"), out int ns) ? ns : 20;
        _report = new ReportWriter(_paths.ReportDir, _paths.PassrateDir, dayStart, nightStart);
        _logFile = new LogWriter(_paths.LogDir);

        Text = VietnameseText.GetApplicationName(_config.Get("Application", "AppName", VietnameseText.ApplicationName));
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1450, 900);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 250, 252);

        // Nút bấm - START/STOP bằng cách bấm vào khung TEST RESULT (_status.Click dưới đây),
        // không còn nút START/STOP + checkbox như giao diện cũ.
        StyleHeader();
        BuildHomeLayout();
        BuildGrid();
        BuildCharts();
        StyleLog();
        StyleStatus();

        // Giữ bảng thông tin gọn bên phải, dành phần còn lại cho kết quả và biểu đồ.
        Shown += (_, _) => ApplyHomeColumnSplit();

        _btnSelectProgram.Click += (_, _) => SelectProgram();
        _btnReconnect.Click += async (_, _) => await ReconnectAsync();
        _btnOpenLog.Click += (_, _) => OpenLogFolder();
        BuildSettingsTab();
        _btnScanHipot.Click += (_, _) => ScanSerialPorts();
        _btnScanCb.Click += (_, _) => ScanSerialPorts();

        // Rời tab Cài đặt thì tự ghi lại file cấu hình (chỉ khi đang mở khóa)
        _tabs.Selecting += (_, e) =>
        {
            if (e.TabPage != _tabSettings && _settingsAccess.Unlocked)
            {
                if (!SaveSettings(silent: true)) e.Cancel = true;
            }
        };

        // Bắt sự kiện từ hai thiết bị
        _status.Click += (_, _) => { if (_running || _fixtureStartPending) RequestStop(); else StartTest(); };

        FormClosing += (_, _) => Shutdown();

        DisableSettings(true);
        LoadSettingsToFields();

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) =>
            _lblClock.Text = $"{DateTime.Now:yyyy/MM/dd}\r\n{DateTime.Now:HH:mm:ss}";
        _clockTimer.Start();
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        // Font to + row cao để đọc được kết quả từ xa - mỗi dòng là MỘT phép thử.
        _grid.Font = new Font("Segoe UI", 14F);
        _grid.RowTemplate.Height = (int)(44 * DeviceDpi / 96F);
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = (int)(56 * DeviceDpi / 96F);
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(30, 58, 95),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            WrapMode = DataGridViewTriState.True
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.GridColor = Color.FromArgb(226, 232, 240);
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

        // 12 cột giống hệt tiêu đề của báo cáo Hipot_GPT9801.
        // Mỗi dòng = 1 phép thử, sắp MỚI NHẤT Ở TRÊN CÙNG (xem AddResultRow).
        string[] cols =
        {
            "Name", "V test (kV)", "Freq (Hz)", "Time test (S)", "Ramup test (S)", "Ref (mA)",
            "Hi set (mA)", "Low set (mA)", "Voltage result (kV)", "Current result (mA)",
            "Time measure (S)", "Result"
        };
        string[] headers =
        {
            "Chương trình", "Điện áp thử (kV)", "Tần số (Hz)", "Thời gian thử (s)",
            "Thời gian tăng áp (s)", "Dòng tham chiếu (mA)", "Ngưỡng trên (mA)", "Ngưỡng dưới (mA)",
            "Điện áp đo (kV)", "Dòng điện đo (mA)", "Thời gian đo (s)", "Kết quả"
        };
        for (int i = 0; i < cols.Length; i++) _grid.Columns.Add(cols[i], headers[i]);
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.MinimumWidth = 74;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        _grid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        _grid.Columns[0].FillWeight = 115;
        _grid.Columns[8].FillWeight = 120;
        _grid.Columns[9].FillWeight = 120;
        _gridPanel.Controls.Add(_grid);
    }

    /// <summary>
    /// Dựng 3 biểu đồ kết quả NGAY TRONG TAB HOME (hàng giữa, cột trái) - không còn tab riêng:
    ///   - PassRateBarChart : cột TOTAL (xanh dương) / PASS (xanh lá) / FAIL (đỏ)
    ///   - YieldPieChart    : vòng tròn PASS / FAIL kèm % và FPY của ca hiện tại
    ///   - CurrentTrendChart: cột xu hướng dòng rò mA của 60 phép thử gần nhất,
    ///     nét đứt = ngưỡng Hi/Low set của chương trình
    /// Dữ liệu đọc từ passrate của ca (model + ngày + Ngày/Đêm), cập nhật sau mỗi phép thử.
    /// Controls tự vẽ (ResultCharts.cs) - không cần thư viện ngoài.
    /// </summary>
    private void BuildCharts()
    {
        foreach (Control chart in new Control[] { _chartPassRate, _chartPie, _chartTrend })
            ScaleDashboard(chart, DeviceDpi / 96F);
        _chartPassRate.Dock = DockStyle.Fill;
        _chartPie.Dock = DockStyle.Fill;
        _chartTrend.Dock = DockStyle.Fill;
        _chartsLayout.Controls.Add(_chartPassRate, 0, 0);
        _chartsLayout.Controls.Add(_chartPie, 1, 0);
        _chartsLayout.Controls.Add(_chartTrend, 2, 0);
        RefreshCharts();
    }

    /// <summary>Vẽ lại 3 biểu đồ (an toàn gọi từ thread bất kỳ).</summary>
    private void RefreshCharts()
    {
        void apply()
        {
            _chartPassRate.SetData(_stats.Total, _stats.Pass, _stats.Fail);
            _chartPie.SetData(_stats.Pass, _stats.Fail, _stats.FpyPercent());
            _chartTrend.SetData(_stats.Points, _program.HighSetMa, _program.LowSetMa);
        }
        if (_chartPassRate.InvokeRequired || _chartPie.InvokeRequired || _chartTrend.InvokeRequired)
            _chartPassRate.BeginInvoke((Action)apply);
        else
            apply();
    }

    private void StyleLog()
    {
        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.BackColor = Color.FromArgb(25, 25, 25);
        _log.ForeColor = Color.FromArgb(180, 220, 180);
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Consolas", 9F);
        _logPanel.Controls.Add(_log);
    }

    private void StyleStatus()
    {
        _status.Font = new Font("Segoe UI", 40F, FontStyle.Bold);
        _status.ForeColor = Color.White;
        _status.BackColor = ColorIdle;
        _status.TextAlign = ContentAlignment.MiddleCenter;
        _status.Cursor = Cursors.Hand;
        _status.BorderStyle = BorderStyle.FixedSingle;
        _status.SizeChanged += (_, _) => FitStatusFont();
    }

    private async void MainForm_Load(object? sender, EventArgs e)
        => await RunStartupChecksAsync();

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyHomeColumnSplit();
    }

    /// <summary>
    /// Bảng bên phải rộng 350 px ở 100% DPI; cột trái nhận toàn bộ phần còn lại.
    /// Tăng chiều cao theo cửa sổ, giữ cuộn dọc khi màn hình thấp.
    /// </summary>
    private void ApplyHomeColumnSplit()
    {
        if (_homeLayout is null || _homeLayout.ColumnStyles.Count < 2) return;

        float scale = DeviceDpi / 96F;
        _homeLayout.ColumnStyles[0].SizeType = SizeType.Percent;
        _homeLayout.ColumnStyles[0].Width = 100F;
        _homeLayout.ColumnStyles[1].SizeType = SizeType.Absolute;
        _homeLayout.ColumnStyles[1].Width = 350F * scale;
        _homeLayout.Height = Math.Max((int)(690 * scale),
            _tabHome.ClientSize.Height - _tabHome.Padding.Vertical);
        _homeLayout.RowStyles[2].Height = 116F * scale;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5 && !_running) { StartTest(); return true; }
        if (keyData == Keys.Escape && (_running || _fixtureStartPending)) { RequestStop(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    #region Bước 2 - Kiểm tra kết nối GPT-9801 + Control Board

    /// <summary>Bước 2 - dò và kết nối cả hai thiết bị, đăng ký sự kiện, nạp chương trình.</summary>
    private async Task RunStartupChecksAsync()
    {
        // All callers run on the UI thread. Take the gate before the first await
        // so another click cannot disconnect/reopen ports used by this attempt.
        if (_connecting || _shuttingDown || _running || _fixtureStartPending || _returningToIdle) return;
        _connecting = true;
        _pollTimer?.Stop();
        _arduino.EventReceived -= OnBoardEvent;
        _arduino.RawLineReceived -= OnBoardLine;
        _hipot.EventReceived -= OnHipotEvent;
        SetConnectionBusy(true);
        try
        {
            _licenseAllowsTest = _license.CanStartTest();
            AddLog("[Bước 2] Đang kiểm tra kết nối GPT-9801 và Control Board...");
            LoadActiveProgram();
            _hipotOk = await Task.Run(ConnectHipot);
            if (_shuttingDown) return;
            if (_hipotOk) await Task.Run(ApplyProgramToHipot);
            if (_shuttingDown) return;
            _cbOk = await Task.Run(ConnectControlBoard);
            if (_shuttingDown) return;
            SetCombo(_hipotPort, _setting.ComPortGPT);
            SetCombo(_cbPort, _setting.ComPortControl);
            HookDeviceEvents(_cbOk);
            if (!_hipotOk || !_cbOk)
            {
                AddLog("[LỖI] Chưa kết nối đủ thiết bị - kiểm tra dây nối, trình điều khiển và cổng COM.");
                AddLog("       Máy thử dùng Config\\SystemSetting.xml [ComPortGPT], bảng điều khiển dùng [ComPortControl].");
                SetStatus("NO DEV", ColorError, "Không kết nối đủ thiết bị. Kiểm tra kết nối rồi chạy lại.");
                return;
            }
            SetStatus("IDLE", ColorIdle, "Đồ gá sẵn sàng. Đặt sản phẩm vào đồ gá để bắt đầu.");
        }
        catch (Exception ex)
        {
            _hipotOk = false;
            _cbOk = false;
            AddLog("[KẾT NỐI] Không hoàn tất kết nối: " + ex.Message);
            if (!_shuttingDown) SetStatus("NO DEV", ColorError, "Không hoàn tất kết nối. Kiểm tra nhật ký rồi kết nối lại.");
        }
        finally
        {
            _connecting = false;
            if (!_shuttingDown && !IsDisposed) SetConnectionBusy(false);
        }
    }

    /// <summary>Nút "Kết nối lại" - dò lại cả hai thiết bị mà không cần khởi động lại app.</summary>
    private async Task ReconnectAsync()
    {
        if (_connecting || _shuttingDown) return;
        if (_running || _fixtureStartPending || _returningToIdle)
        {
            AddLog("[KẾT NỐI] Đang chạy phép thử - không dò lại được.");
            return;
        }
        if (!SaveSettingsCore(silent: false, applyToConnectedBoard: false)) return;
        await RunStartupChecksAsync();
        if (_hipotOk && _cbOk)
            AddLog("[KẾT NỐI] Dò lại thành công cả hai thiết bị.");
        else
            AddLog("[KẾT NỐI] Vẫn chưa đủ thiết bị - kiểm tra lại Config\\SystemSetting.xml.");
    }

    /// <summary>
    /// Đăng ký sự kiện từ HIPOT + Control Board. Dùng <c>-=</c> trước <c>+=</c>
    /// để không bị dính handler 2 lần khi người dùng bấm "Kết nối lại".
    /// </summary>
    private void HookDeviceEvents(bool cbOk)
    {
        _hipot.EventReceived -= OnHipotEvent;
        _hipot.EventReceived += OnHipotEvent;

        _arduino.EventReceived -= OnBoardEvent;
        _arduino.RawLineReceived -= OnBoardLine;

        if (!cbOk) return;
        _arduino.EventReceived += OnBoardEvent;
        _arduino.RawLineReceived += OnBoardLine;
        // Bật sự kiện (EVT ...) rồi báo bảng điều khiển về trạng thái sẵn sàng
        // (giao thức cũ của Hipot_GPT9801).
        _arduino.Send("EVENTS ON", 800);
        _arduino.Send("*R", 800);
        if (_fixtureMode) ApplyBoardTimings(_setting.Timing);
        StartPolling();
    }

    /// <summary>Kết nối thật tới GPT-9801 qua RS-232, xác thực bằng *IDN?.</summary>
    protected virtual bool ConnectHipot()
    {
        string port = _setting.ComPortGPT.Trim();
        int baud = _setting.ComBaudrateGPT;
        AddLog($"[HIPOT] Đang kết nối {Describe(port, baud, _setting.ComDataBitGPT, _setting.ComParityGPT, _setting.ComStopBitGPT)}...");

        bool isAuto = port.Equals("AUTO", StringComparison.OrdinalIgnoreCase) || port.Length == 0;
        string[] excluded = new[] { _setting.ComPortControl, _arduino.PortName ?? "" };
        string err;
        bool ok;
        if (isAuto)
            ok = _hipot.AutoConnect(baud, _setting.ParityGPT, _setting.ComDataBitGPT, _setting.StopBitsGPT,
                out _, out err, excludedPorts: excluded);
        else
        {
            ok = _hipot.Connect(port, baud, _setting.ParityGPT, _setting.ComDataBitGPT, _setting.StopBitsGPT, out err);
            if (!ok)
            {
                string directError = err;
                AddLog($"[HIPOT] Cổng {port} không kết nối được ({err}) - dò lại cổng máy đo...");
                ok = _hipot.AutoConnect(baud, _setting.ParityGPT, _setting.ComDataBitGPT, _setting.StopBitsGPT,
                    out _, out err, excludedPorts: excluded.Append(port));
                if (!ok) err = $"{port}: {directError} | {err}";
            }
        }

        _hipotOk = ok;
        if (ok)
        {
            string found = _hipot.PortName ?? "";
            if (found.Length > 0 && !found.Equals(_setting.ComPortGPT, StringComparison.OrdinalIgnoreCase))
            {
                _setting.ComPortGPT = found;
                try { _setting.Save(_paths.SettingFile); AddLog($"[HIPOT] Đã lưu cổng máy đo {found} cho lần kết nối sau."); }
                catch (Exception ex) { AddLog("[HIPOT] Không lưu được cổng máy đo: " + ex.Message); }
            }
            AddLog($"[HIPOT] [THÀNH CÔNG] {_hipot.PortName} -> {_hipot.Idn}");
            SetLinkState(_valHipot, $"{_hipot.PortName} - Đã kết nối", ColorOk);
        }
        else
        {
            AddLog($"[HIPOT] [LỖI] {err}");
            AddLog("       Gợi ý: đổi cổng / tốc độ truyền / kiểm tra chẵn lẻ / số bit dữ liệu trong Config\\SystemSetting.xml.");
            AddLog("       Lưu ý bảng Arduino dùng cổng COM khác (ComPortControl) - đừng ghi lẫn.");
            SetLinkState(_valHipot, "Chưa kết nối", ColorError);
        }
        return ok;
    }

    /// <summary>
    /// Kết nối bảng điều khiển Arduino.
    ///
    /// Nếu <c>ComPortControl=AUTO</c> thì quét toàn bộ cổng serial. Việc quét này trên
    /// máy có cổng Bluetooth mất hàng chục giây (mỗi <c>SerialPort.Open()</c> trên cổng
    /// BthModem có thể tốn nhiều giây). Vì vậy sau lần dò được đầu tiên, phần mềm tự
    /// GHIM cổng đó vào <c>Config\SystemSetting.xml</c> để các lần mở sau mở thẳng,
    /// không phải quét. Nếu cổng đã ghim mà mất (unplug) thì quét lại một lần rồi ghim lại.
    /// </summary>
    protected virtual bool ConnectControlBoard()
    {
        _fixtureTimingReady = false;
        string port = _setting.ComPortControl.Trim();
        int baud = _setting.ComBaudrateControl;
        bool isAuto = port.Equals("AUTO", StringComparison.OrdinalIgnoreCase) || port.Length == 0;
        AddLog($"[CB] Đang kết nối {Describe(port, baud, _setting.ComDataBitControl, _setting.ComParityControl, _setting.ComStopBitControl)}...");

        string err;
        bool ok;
        if (isAuto)
        {
            ok = _arduino.AutoConnect(baud, out _, out err,
                excludedPorts: new[] { _hipot.PortName ?? "", _setting.ComPortGPT });
        }
        else
        {
            ok = _arduino.Connect(port, baud, out err);
            if (!ok)
            {
                AddLog($"[CB] Cổng {port} không phản hồi ({err}) - quét lại toàn bộ cổng nối tiếp...");
                ok = _arduino.AutoConnect(baud, out _, out err,
                    excludedPorts: new[] { port, _hipot.PortName ?? "", _setting.ComPortGPT });
            }
        }

        _cbOk = ok;
        if (ok)
        {
            string found = _arduino.PortName ?? "";
            if (found.Length > 0 && !found.Equals(_setting.ComPortControl, StringComparison.OrdinalIgnoreCase))
            {
                _setting.ComPortControl = found;
                try
                {
                    _setting.Save(_paths.SettingFile);
                    AddLog($"[CB] [THÀNH CÔNG] Đã ghim cổng {found} vào Config\\SystemSetting.xml " +
                           "- lần mở sau mở thẳng, không phải quét nữa.");
                }
                catch (Exception ex)
                {
                    AddLog($"[CB] [CẢNH BÁO] Không ghi được ghim cổng: {ex.Message}");
                }
            }
            AddLog($"[CB] [THÀNH CÔNG] {found} - {_arduino.BoardId}");
            AddLog($"[CB] [THÀNH CÔNG] Phiên bản phần mềm điều khiển: {_arduino.Firmware}");
            ArduinoLink.Reply st = _arduino.SendCommand("STATUS?", 1500);
            if (st.Ok) AddLog($"[CB] Trạng thái: {st.Text}");

            // Nhận diện chế độ fixture tự động từ STATUS? (firmware v1.2.0 có "FIX=..").
            // v1.3.0: STATUS? trả FIXTURE=/LOCK= (bản cũ là FIX=). Chấp nhận cả 3 để
            // phần mềm tự nhận diện chế độ fixture dù nâng cấp firmware từ bản cũ.
            _fixtureMode = (st.Ok && (st.Text.Contains("FIXTURE=") || st.Text.Contains("FIX="))) ||
                           (_arduino.Firmware?.Contains("FW=1.4") == true) ||
                           (_arduino.Firmware?.Contains("FW=1.3") == true) ||
                           (_arduino.Firmware?.Contains("FW=1.2") == true);
            AddLog(_fixtureMode
                ? "[CB] [THÀNH CÔNG] CHẾ ĐỘ ĐỒ GÁ TỰ ĐỘNG: bắt đầu → xilanh đóng → kim tiếp xúc chạm sản phẩm → " +
                  "tự phát áp → kết quả → xilanh rút lên."
                : "[CB] [THÔNG TIN] Chế độ thủ công: cảm biến nắp an toàn + cảm biến sản phẩm được kiểm " +
                  "tại thời điểm bấm bắt đầu.");
            SetLinkState(_valCb, $"{found} - Đã kết nối", ColorOk);
        }
        else
        {
            _fixtureMode = false;
            AddLog($"[CB] [LỖI] {err}");
            if (isAuto)
                AddLog("       Gợi ý: ghi rõ ComPortControl (vd COM6) vào Config\\SystemSetting.xml " +
                       "để bỏ qua bước quét toàn bộ cổng nối tiếp.");
            SetLinkState(_valCb, "Chưa kết nối", ColorError);
        }
        return ok;
    }

    private void SetLinkState(Label l, string text, Color c)
    {
        void apply() { l.Text = text; l.ForeColor = c; }
        if (l.InvokeRequired) l.BeginInvoke((Action)apply); else apply();
    }

    /// <summary>Gửi tham số chương trình hiện tại lên máy hipot.</summary>
    private void ApplyProgramToHipot()
    {
        if (!_hipot.IsOpen) return;
        if (!_hipot.SetProgram(_program, out string err))
        {
            AddLog($"[HIPOT] [LỖI] Không nạp được tham số chương trình: {err}");
            return;
        }
        AddLog($"[HIPOT] Đã nạp chương trình: {_program}");
    }

    private void LoadActiveProgram()
    {
        _programFile = _paths.ReadActiveProgramFile();
        string path = _paths.ActiveProgramPath;
        TestProgram? p = TestProgram.Load(path);
        if (p is null)
        {
            p = TestProgram.CreateDefault();
            p.Save(path);
            AddLog($"[CT] Chưa có chương trình - đã tạo mặc định: {path}");
        }
        _program = p;
        _programFile = Path.GetFileName(path);
        _paths.WriteActiveProgramFile(_programFile);
        _valProgram.Text = _programFile;
        _valModel.Text = _program.Name;
        AddLog($"[CT] Chương trình đang dùng: {_programFile} -> {_program}");

        var st = _report.LoadPassrate(_program.Name, DateTime.Now);
        _total = st.Total; _pass = st.Pass; _fail = st.Fail;
        // Biểu đồ: nạp đếm TOTAL/PASS/FAIL của ca từ passrate (giống LoadPassRate +
        // ShowPassrate của app cũ) để mở app giữa ca vẫn thấy đúng số liệu.
        // Biểu đồ xu hướng + lưới lịch sử: nạp phép thử GẦN NHẤT từ báo cáo CSV của ca
        // (ReadRecentResults / ReadRecentRows) - mở app giữa ca vẫn thấy lịch sử, không bắt đầu từ trắng.
        IReadOnlyList<TestResultPoint> recent = _report.ReadRecentResults(_program.Name, DateTime.Now);
        _stats.Load(st.Pass, st.Fail, recent);
        UpdateStats(0);
        LoadGridHistory();
        RefreshCharts();
    }

    #endregion

    #region Sự kiện từ Control Board

    /// <summary>
    /// Dòng thô từ board - chỉ bắt giao thức cũ *TESTON# / *CANCEL#.
    /// Các dòng EVT / OK BOOT đã được OnBoardEvent xử lý nên không log lại ở đây.
    /// </summary>
    private void OnBoardLine(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => OnBoardLine(line))); return; }

        if (line.Contains("*TESTON#"))
        {
            StartTest(fromBoard: true);
        }
        else if (line.Contains("*CANCEL#"))
        {
            if (!_running && !_licenseAllowsTest) { _fixtureStartPending = false; return; }
            AddLog("[CB] Nhận yêu cầu dừng khẩn từ bảng điều khiển / nắp an toàn bị mở.");
            RequestStop(fromBoard: true);
        }
    }

    private void OnBoardEvent(string evt)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => OnBoardEvent(evt))); return; }
        if (evt.StartsWith("OK BOOT", StringComparison.Ordinal))
        {
            _fixtureTimingReady = false;
            _fixtureStartPending = false;
            _timingStatus.Text = "Bảng điều khiển vừa khởi động lại. Kết nối lại để gửi thời gian đã lưu.";
            _timingStatus.ForeColor = Color.DarkOrange;
            AddLog("[CT] " + _timingStatus.Text);
            return;
        }
        if (evt.Contains("CYCLE_CLOSING", StringComparison.OrdinalIgnoreCase) && !_running)
            _fixtureStartPending = true;
        if (evt.Contains("CYCLE_IDLE", StringComparison.OrdinalIgnoreCase)) _fixtureStartPending = false;
        if (!_running && !_licenseAllowsTest) return;
        AddLog($"[CB] {evt}");
        if (evt.Contains("INTERLOCK_OPEN", StringComparison.OrdinalIgnoreCase) ||
            evt.Contains("UNIT_LOST", StringComparison.OrdinalIgnoreCase) ||
            evt.Contains("RELAY_TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
            evt.Contains("STROKE_TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            // Chỉ đổi trạng thái khi đang chạy phép thử; khi idle mở nắp là bình thường.
            if (_running)
                SetStatus("STOP", ColorError, "An toàn kích hoạt - phép thử bị ngắt bởi bảng điều khiển.");
            AddLog(evt.Contains("STROKE_TIMEOUT", StringComparison.OrdinalIgnoreCase)
                ? "[CB] ⚠ XILANH KHÔNG ĐI HẾT HÀNH TRÌNH - đã cắt hết và rút về. " +
                  "Kiểm tra áp khí / van điện từ / xilanh bị kẹt TRƯỚC KHI thử tiếp."
                : "[CB] Điều kiện an toàn bị vi phạm - luôn kiểm tra lại trước khi bắt đầu.");
        }
    }

    /// <summary>Dòng từ GPT-9801 đẩy lên không theo yêu cầu (InterLock / ERROR).</summary>
    private void OnHipotEvent(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => OnHipotEvent(line))); return; }
        AddLog($"[HIPOT] {line}");
        if (line.Contains("InterLock", StringComparison.OrdinalIgnoreCase))
        {
            _stopRequested = true;
            SetStatus("MACHINE ERROR", ColorError, "GPT-9801 báo lỗi liên động - nắp an toàn chưa đóng.");
        }
    }

    /// <summary>Poll định kỳ trạng thái interlock / unit / xilanh từ bảng điều khiển.</summary>
    private void StartPolling()
    {
        _pollTimer?.Stop(); _pollTimer?.Dispose();
        _pollTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _pollTimer.Tick += (_, _) =>
        {
            if (!_arduino.IsOpen || _running) return;

            if (_fixtureMode)
            {
                // Chế độ fixture: board tự quyết an toàn (D8 "cho phép đo" + D13), nên PC chỉ
                // poll trạng thái xilanh để operator thấy rõ đang Ở TRÊN / ĐANG XUỐNG / ĐANG LÊN.
                ArduinoLink.Reply fx = _arduino.SendCommand("FIX STATUS", 300);
                if (!fx.Ok) return;
                string s = ArduinoLink.GetField(fx.Text, "STATE");
                if (s.Equals(_lastFixState, StringComparison.OrdinalIgnoreCase)) return;
                _lastFixState = s;
                if (!_licenseAllowsTest) return;
                AddLog($"[Bước 3] Xilanh: {DescribeFixState(s)}");
                return;
            }

            ArduinoLink.Reply r = _arduino.SendCommand("INTERLOCK?", 300);
            if (!r.Ok) return;
            bool closed = r.Text.Contains("CLOSED", StringComparison.OrdinalIgnoreCase);
            if (closed != _lastInterlock)
            {
                _lastInterlock = closed;
                AddLog($"[Bước 3] Nắp an toàn: {(closed ? "ĐÓNG" : "MỞ")}");
            }
        };
        _pollTimer.Start();
    }

    private static string DescribeFixState(string s) => s.ToUpperInvariant() switch
    {
        "CLOSING"  => "ĐỒ GÁ ĐANG ĐÓNG (kẹp đang khép lại)",
        "POGO_OUT" => "KIM TIẾP XÚC ĐANG ĐẨY RA (tiếp cận sản phẩm)",
        "ARMED"    => "ĐÃ KHÓA LIÊN ĐỘNG GPT-9800 - chờ máy tính cho phép phóng áp",
        "TESTING"  => "RƠ-LE CAO ÁP ĐÃ ĐÓNG - GPT-9800 đang phóng áp vào sản phẩm",
        "POGO_BACK" => "KIM TIẾP XÚC ĐANG RÚT VỀ",
        "FIX_OPEN" => "ĐỒ GÁ ĐANG MỞ RA",
        "PASS"     => "PASS - đồ gá đã mở",
        "FAIL"     => "FAIL - đồ gá đã mở",
        "ABORTED"  => "ĐÃ DỪNG KHẨN - đồ gá đang mở",
        "FAULT"    => "LỖI CƠ KHÍ - mọi van đã tắt, KHÔNG thử tiếp",
        "IDLE"     => "SẴN SÀNG - đồ gá mở, chờ sản phẩm kế tiếp",
        _ => "Chưa xác định"
    };

    #endregion

    #region Bước 3/4 - Đặt unit và nhận START TEST

    /// <summary>
    /// Kiểm tra điều kiện an toàn chế độ THỦ CÔNG (legacy, không có fixture tự động):
    /// hỏi board STATUS? xem nắp an toàn đã ĐÓNG và unit đã PRESENT chưa.
    /// Trả về true = đủ điều kiện. Board trả BYPASS (hoặc mất kết nối board) cũng cho
    /// qua - khi đó GPT-9801 tự bảo vệ bằng InterLock nội bộ.
    /// Giao diện mới không còn checkbox "Unit đã vào fixture" nên kiểm tra TRỰC TIẾP
    /// tại thời điểm bấm START (chính xác hơn tick tay từ trước).
    /// </summary>
    private bool CheckLegacyConditions()
    {
        if (!_arduino.IsOpen) return true;
        ArduinoLink.Reply st = _arduino.SendCommand("STATUS?", 800);
        if (!st.Ok) return true;
        string il = ArduinoLink.GetField(st.Text, "INTERLOCK");
        string un = ArduinoLink.GetField(st.Text, "UNIT");
        bool bypass = _fixtureMode || il.Equals("BYPASS", StringComparison.OrdinalIgnoreCase);
        if (bypass) return true;
        if (!il.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
        {
            AddLog("[Bước 4] Chưa đạt: nắp an toàn đang MỞ.");
            return false;
        }
        if (!un.Equals("PRESENT", StringComparison.OrdinalIgnoreCase))
        {
            AddLog("[Bước 4] Chưa đạt: chưa phát hiện sản phẩm trên đồ gá.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Bước 4.5 - KHÓA LIÊN ĐỘNG (chỉ chạy khi firmware v1.3.0 ở chế độ fixture).
    ///
    /// Board đã short tiếp điểm INTERLOCK1(1) - INTERLOCK2(2) trên DB-9 SIGNAL I/O của
    /// GPT-9800 ngay khi fixture đóng + pogo chạm unit (*TESTON# / EVT HV_ARMED). Lệnh
    /// "HV ARM" là PHẦN XÁC NHẬN CỦA PC: chỉ khi PC gửi thì board mới đóng relay D4 -
    /// bước cuối cùng mở đường phóng điện. Như vậy 2 điều kiện phải ĐÚNG cùng lúc:
    ///   (1) cơ khí đã kín (board tự quyết)  và  (2) PC cho phép (đây).
    /// Không có lệnh SCPI interlock trên GPT-9800 nên PC không thể làm phần này thay board.
    /// </summary>
    private bool ArmHighVoltage()
    {
        ArduinoLink.Reply r = _arduino.SendCommand("HV ARM", 1500);
        if (!r.Ok)
            AddLog($"[LỖI] Bảng điều khiển KHÔNG cho phép phóng áp: {r.Raw}");
        return r.Ok;
    }

    /// <summary>
    /// Bước 4/5 - bắt đầu phép thử. <paramref name="fromBoard"/> = true khi tới từ
    /// nút START vật lý (*TESTON#) - khi đó board ĐÃ TỰ kiểm interlock + unit rồi nên
    /// không kiểm lại điều kiện ở đây.
    /// Khởi động từ màn hình: bấm vào khung TEST RESULT (hoặc F5).
    /// </summary>
    private async void StartTest(bool fromBoard = false)
    {
        if (_connecting || _shuttingDown)
        {
            if (fromBoard && _arduino.IsOpen) _arduino.SendCommand("TEST ABORT", 800);
            return;
        }
        if (_running) return;
        if (_returningToIdle || (!fromBoard && _fixtureStartPending))
        {
            if (fromBoard && _arduino.IsOpen) _arduino.SendCommand("TEST ABORT", 800);
            return;
        }
        if (fromBoard) _fixtureStartPending = false;
        _licenseAllowsTest = _license.CanStartTest(out bool expired);
        if (!_licenseAllowsTest)
        {
            // Nhả đồ gá TRƯỚC thông báo vì START vật lý có thể đã đóng đồ gá.
            // Không cấp cao áp hoặc tạo kết quả FAIL khi quyền START bị từ chối.
            if (fromBoard && _arduino.IsOpen)
                _arduino.SendCommand("TEST ABORT", 800);
            if (expired) ReportExpiredStart();
            return;
        }
        if (_fixtureMode && !_fixtureTimingReady)
        {
            if (fromBoard && _arduino.IsOpen) _arduino.SendCommand("TEST ABORT", 800);
            _retainTestResult = false;
            SetStatus("NOT READY", ColorError, "Bảng điều khiển chưa xác nhận thời gian. Kiểm tra trong tab Cài đặt.");
            ShowStartError("Bảng điều khiển chưa xác nhận thời gian chu trình. Vào Cài đặt, kiểm tra phần mềm điều khiển rồi lưu hoặc kết nối lại.");
            return;
        }
        if (fromBoard) AddLog("[CB] Nhận yêu cầu bắt đầu từ nút bấm trên bảng điều khiển.");

        if (!_hipotOk || !_hipot.IsOpen)
        {
            AddLog("[Bước 4] Không thể bắt đầu: chưa kết nối GPT-9801.");
            // Board có thể vừa vào ST_ARMED (nhận *TESTON#) - nhả ngay, KHÔNG để fixture
            // nằm kín + liên động khóa trong lúc dialog hiển thị và chờ ARM_WAIT_MS.
            if (_arduino.IsOpen)
            {
                ArduinoLink.Reply ab = _arduino.SendCommand("TEST ABORT", 800);
                if (ab.Ok) AddLog($"[CB] Đã nhả đồ gá do thiếu máy thử: {ab.Text}");
            }
            DialogResult dr = VietnameseMessageBox.Show(
                "Chưa kết nối GPT-9801. Kiểm tra cáp RS-232 và cổng COM.\n\n" +
                "Bấm Có để dò lại kết nối ngay?",
                "Không đủ điều kiện kiểm tra", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.Yes) await ReconnectAsync();
            return;
        }

        // Một yêu cầu START hợp lệ mới thay kết quả đang giữ bằng quy trình kế tiếp.
        _retainTestResult = false;
        SetStatus("PREPARING", ColorTesting, "Đang chuẩn bị phép thử tiếp theo...");

        if (fromBoard)
        {
            // Board đã tự chạy chu trình fixture (đóng xilanh) rồi mới gửi *TESTON#.
            // KHÔNG kiểm interlock/unit ở đây vì board chính là nơi thực thi an toàn.
        }
        else if (_fixtureMode && _arduino.IsOpen)
        {
            // Chế độ fixture tự động: START từ màn hình cũng phải đi qua
            // chu trình xilanh của board để cả hai đường khởi động dùng chung
            // MỘT nguồn sự thật duy nhất. Board sẽ tự báo *TESTON# khi xilanh đóng.
            ArduinoLink.Reply fx = _arduino.SendCommand("FIX START", 2000);
            if (!fx.Ok)
            {
                AddLog($"[LỖI] Bảng điều khiển từ chối khởi động đồ gá: {fx.Raw}");
                SetStatus("FAIL", ColorFail, "Không khởi động được chu trình đồ gá.");
                return;
            }
            SetStatus("PREPARING", ColorTesting, "Đang đóng đồ gá và đưa kim tiếp xúc vào vị trí đo...");
            _fixtureStartPending = true;
            return;                       // chu trình tự chạy tiếp khi nhận *TESTON#
        }
        else if (!CheckLegacyConditions())
        {
            // Chế độ thủ công: kiểm interlock + unit TRỰC TIẾP tại thời điểm START
            // (thay cho checkbox tick tay ở giao diện cũ).
            SetStatus("NOT READY", ColorIdle, "Chưa đạt điều kiện an toàn - xem nhật ký kiểm tra.");
            return;
        }

        _running = true;
        CycleTimingSettings timing = _setting.Timing;
        _stopRequested = false;
        _status.Cursor = Cursors.Default;
        _cycleWatch.Restart();
        SetStatus("TESTING", ColorTesting, "GPT-9801 đang thực hiện phép thử...");
        AddLog($"[Bước 5] Bắt đầu phép thử: {_program}");

        try
        {
            // Gửi lệnh bắt đầu lên máy. Khi fromBoard=true thì board ĐÃ tự đóng
            // xilanh và ĐÃ mở relay rồi mới gửi *TESTON# -> gửi TEST START lần nữa
            // sẽ bị firmware từ chối "ERR TEST_ALREADY_RUNNING" và báo FAIL oan.
            if (_arduino.IsOpen && !fromBoard)
            {
                ArduinoLink.Reply start = _arduino.SendCommand("TEST START", 1500);
                if (!start.Ok)
                {
                    AddLog($"[Bước 4] Bảng điều khiển TỪ CHỐI bắt đầu: {start.Raw}");
                    SetStatus("FAIL", ColorFail, "Bảng điều khiển từ chối bắt đầu: kiểm tra nắp an toàn / vị trí sản phẩm.");
                    await Task.Delay(timing.ErrorHoldMs);
                    return;
                }
            }

            // Giai đoạn khóa liên động: board đang ở ST_ARMED (fixture kín + pogo chạm +
            // DB-9 pin1-2 short) -> HV ARM mới đóng relay D4 cho GPT-9800 phóng áp.
            if (_fixtureMode && _arduino.IsOpen && !ArmHighVoltage())
            {
                SetStatus("FAIL", ColorFail, "Bảng điều khiển từ chối mở đường phóng áp - kiểm tra liên động.");
                if (_hipot.IsOpen) _hipot.StopTest(out _);
                try { _arduino.SendCommand("TEST ABORT", 800); } catch { }
                await Task.Delay(timing.ErrorHoldMs);
                return;
            }

            if (!_hipot.IsOpen) throw new InvalidOperationException("Mất kết nối GPT-9801.");

            // 1) Bật phép thử trên máy
            string startErr = "";
            bool started = await Task.Run(() => _hipot.StartTest(out startErr));
            if (!started) throw new InvalidOperationException($"Không khởi động được phép thử: {startErr}");

            // 2) Chờ máy chạy xong (ramp + test time) rồi đọc kết quả thật.
            //    Không đọc MEAS? trước khi đã đủ thời lượng ramp+test, vì query
            //    "FUNC:TEST ON;:FUNC:TEST?" có thể trả "OK" ngay khi máy nhận lệnh.
            int minMs = (int)((_program.RampSec + _program.TimeSec) * 1000) + timing.MeasurementReadyDelayMs;
            string? doneLine = await WaitHipotAsync(
                l => l.Contains("OK", StringComparison.OrdinalIgnoreCase),
                timing.CompletionTimeoutMs(_program), minMs);
            if (_stopRequested) { AddLog("[DỪNG] Phép thử bị dừng trước khi đọc kết quả."); return; }
            if (doneLine is null) throw new TimeoutException("GPT-9801 không báo hoàn tất trong thời gian cho phép.");

            HipotResult? res = await Task.Run(() => _hipot.ReadMeasurement(timing.MeasurementReadTimeoutMs));
            if (res is null) throw new TimeoutException("Không đọc được kết quả đo từ GPT-9801.");

            _cycleWatch.Stop();

            bool pass = res.IsPass && !res.IsError;
            SetStatus(pass ? "PASS" : "FAIL", pass ? ColorPass : ColorFail,
                      pass ? "Phép thử đạt." : $"Phép thử không đạt ({VietnameseText.Result(res.Result)}).");
            AddLog(pass
                ? $"[KẾT QUẢ] PASS - {res.VoltageKv:F3} kV / {res.CurrentMa:F2} mA / {res.TimeSec:F2} s"
                : $"[KẾT QUẢ] FAIL - {res.Result}");

            // Báo lại bảng điều khiển để bật đèn + còi (giao thức *PASS# / *FAIL#).
            if (_arduino.IsOpen)
            {
                string ack = pass ? "*PASS#" : "*FAIL#";
                _arduino.SendCommand(ack, 1000);
            }

            // Bước 7 - lưu báo cáo và thống kê ca (SaveResult tăng _total),
            // rồi thêm dòng lịch sử MỚI lên đầu lưới với đúng số thứ tự vừa tăng.
            SaveResult(res, pass);
            AddResultRow(res);
        }
        catch (Exception ex)
        {
            _cycleWatch.Stop();
            AddLog($"[LỖI] {ex.Message}");
            SetStatus("ERROR", ColorError, "Xảy ra lỗi trong quá trình thử. Kiểm tra thiết bị.");
            if (_hipot.IsOpen) _hipot.StopTest(out _);
            if (_arduino.IsOpen) _arduino.SendCommand("TEST ABORT", 800);
        }
        finally
        {
            _running = false;
            _status.Cursor = Cursors.Hand;
            // Bước 8 - Chờ unit tiếp theo.
            await ReturnToIdleAsync();
        }
    }

    private void ReportExpiredStart()
    {
        const string message = "Lỗi phần cứng thiết bị. Vui lòng liên hệ bộ phận kỹ thuật để được hỗ trợ.";
        _retainTestResult = false;
        SetStatus("MACHINE ERROR", ColorError, message);
        if (_showingStartError) return;
        _showingStartError = true;
        try { ShowStartError(message); }
        finally { _showingStartError = false; }
    }

    protected virtual void ShowStartError(string message)
        => VietnameseMessageBox.Show(this, message, "Lỗi thiết bị", MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>
    /// Đợi phản hồi từ GPT-9801 theo lát 100 ms để có thể hủy giữa chừng.
    /// <paramref name="minMs"/> = thời lượng tối thiểu phải chờ (ramp + test time),
    /// tránh đọc MEAS? khi máy vẫn đang phát áp.
    /// </summary>
    private async Task<string?> WaitHipotAsync(Func<string, bool> predicate, int timeoutMs, int minMs = 0)
        => await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            string? found = null;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (_stopRequested) return null;
                if (_hipot.WaitFor(predicate, 100, out string line)) { found = line; break; }
            }
            if (found is null) return null;

            // Đã thấy "OK" nhưng mới chạy chưa đủ ramp+test -> chờ nốt cho đủ,
            // vừa chạy vừa tiêu thụ dòng thừa để hàng đợi phản hồi không bị đầy.
            while (sw.ElapsedMilliseconds < minMs)
            {
                if (_stopRequested) return null;
                _hipot.WaitFor(_ => true, 100, out _);
            }
            return found;
        });

    /// <summary>
    /// Dừng phép thử khẩn cấp. Gửi FUNC:TEST OFF để cắt phát áp NGAY trên máy,
    /// đồng thời báo bảng điều khiển ngắt relay.
    /// </summary>
    private async void RequestStop(bool fromBoard = false)
    {
        if (!_running && !_fixtureStartPending) { AddLog("[DỪNG] Không có phép thử nào đang chạy."); return; }
        bool preparing = _fixtureStartPending && !_running;
        _fixtureStartPending = false;
        _stopRequested = true;
        AddLog($"[DỪNG] Yêu cầu dừng phép thử{(fromBoard ? " từ bảng điều khiển" : " bởi người vận hành")}...");
        if (_hipot.IsOpen) _hipot.StopTest(out _);

        // Không echo lệnh ABORT về cho board khi CHÍNH board là người khởi xướng.
        // Board đã tự cắt relay + tự rút xilanh rồi mới gửi *CANCEL#; gửi TEST ABORT
        // lần nữa sẽ chạy requestAbort() lần 2 -> retract() gọi lại và RESET strokeSinceMs,
        // làm thời gian rút bị cộng dồn vô hạn. Cùng mô-típ với lỗi gửi TEST START sau *TESTON#.
        if (_arduino.IsOpen && !fromBoard) _arduino.SendCommand("TEST ABORT", 800);
        SetStatus("STOP", ColorError, "Phép thử đã dừng. Kiểm tra đồ gá trước khi bắt đầu lần tiếp theo.");
        if (preparing) await ReturnToIdleAsync();
    }

    #endregion

    #region Bước 7 - Lưu kết quả và hoàn tất

    private void SaveResult(HipotResult res, bool pass)
    {
        double cycleSeconds = _cycleWatch.Elapsed.TotalSeconds;
        _total++;
        if (pass) _pass++; else _fail++;

        // Biểu đồ TOTAL/PASS/FAIL + tròn PASS/FAIL: cập nhật ngay sau khi có kết quả.
        _stats.Add(_total, res.CurrentMa, res.Result, DateTime.Now);
        RefreshCharts();

        UpdateStats(cycleSeconds);

        try
        {
            string path = _report.WriteReport(_program.Name, _total, _program, res, DateTime.Now);
            _report.SavePassrate(_program.Name, DateTime.Now, _total, _pass, _fail);
            _ = path;   // đường dẫn chi tiết đã nằm trong file báo cáo CSV của ca.
        }
        catch (Exception ex)
        {
            AddLog($"[LỖI] Không lưu được báo cáo: {ex.Message}");
        }
        AddLog($"[TỔNG] Chu kỳ {cycleSeconds:F2} s | Pass {_pass} | Fail {_fail} | Tổng {_total}");
    }

    private void UpdateStats(double cycleSeconds)
    {
        void apply()
        {
            _valCycleTime.Text = $"{cycleSeconds:F2} s";
            _valFpy.Text = _total == 0 ? "0.00%" : $"{(double)_pass / _total * 100:F2}%";
            _valTotal.Text = _total.ToString();
            _valFail.Text = _fail.ToString();
            _valPass.Text = _pass.ToString();
        }
        if (_valTotal.InvokeRequired) _valTotal.BeginInvoke((Action)apply); else apply();
    }

    #endregion

    #region Bước 8 - Chờ unit tiếp theo

    private async Task ReturnToIdleAsync()
    {
        _returningToIdle = true;
        try
        {
            CycleTimingSettings timing = _setting.Timing;
            // KHÔNG gửi "RESET" ở đây. Lệnh *PASS# / *FAIL# đã làm board:
            //   - tắt relay, bật đèn kết quả
            //   - và BẮT ĐẦU rút xilanh (solUpOn)
            // RESET gửi ngay sau đó sẽ gọi solBothOff() + lampsOff() -> dừng xilanh
            // giữa chừng và tắt luôn đèn vừa bật, khi operator còn chưa thấy kết quả.
            // Board tự về ST_IDLE sau khi rút pogo (POGO_BACK_MS) rồi mở fixture (FIX_OPEN_MS).
            await Task.Delay(timing.PostCycleDelayMs);

            if (_fixtureMode && _arduino.IsOpen)
            {
                // Chờ board rút xilanh xong (STATE=IDLE) rồi mới báo sẵn sàng: bấm START
                // giữa chừng sẽ nhận "ERR FIX_NOT_IDLE" từ board.
                bool idleNow = false;
                var wait = Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < timing.FixtureReturnTimeoutMs && !IsDisposed)
                {
                    int remaining = timing.FixtureReturnTimeoutMs - (int)wait.ElapsedMilliseconds;
                    await Task.Delay(Math.Min(100, remaining));
                    remaining = timing.FixtureReturnTimeoutMs - (int)wait.ElapsedMilliseconds;
                    if (remaining <= 0 || IsDisposed) break;
                    ArduinoLink.Reply fx = _arduino.SendCommand("FIX STATUS", Math.Min(250, remaining));
                    if (fx.Ok && ArduinoLink.GetField(fx.Text, "STATE")
                            .Equals("IDLE", StringComparison.OrdinalIgnoreCase))
                    {
                        idleNow = true;
                        break;
                    }
                }
                if (!idleNow)
                    AddLog("[CẢNH BÁO] Xilanh chưa về vị trí trên - chờ thêm trước khi bắt đầu sản phẩm kế tiếp.");
            }

            // Fixture về trạng thái chờ; TEST RESULT vẫn giữ kết quả và màu của lần vừa đo.
            // Chỉ START tiếp theo mới chuyển hiển thị sang PREPARING / TESTING.
        }
        finally { _returningToIdle = false; }
    }

    private void Shutdown()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        _pollTimer?.Stop(); _pollTimer?.Dispose();
        _clockTimer?.Stop(); _clockTimer?.Dispose();
        try { if (_hipot.IsOpen) _hipot.StopTest(out _); } catch { }
        try { if (_arduino.IsOpen) _arduino.SendCommand("*R", 500); } catch { }
        _hipot.Dispose();
        _arduino.Dispose();
        _logFile.Dispose();
    }

    #endregion

    #region Chương trình phép thử

    private void SelectProgram()
    {
        if (_running) { VietnameseMessageBox.Show("Đang chạy phép thử - không đổi chương trình.", "Thông báo",
            MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var f = new OpenProgramForm(_paths.ProgramDir);
        f.ShowDialog(this);
        if (f.SelectedFile is null) return;
        _programFile = f.SelectedFile;
        _paths.WriteActiveProgramFile(_programFile);
        LoadActiveProgram();
        ApplyProgramToHipot();
        AddLog($"[CT] Đã chuyển sang chương trình: {_programFile}");
    }

    #endregion

    #region Giao diện

    /// <summary>
    /// Cập nhật khung trạng thái lớn + dòng gợi ý nhỏ bên dưới
    /// (không ghi log - chỉ trạng thái quan trọng mới vào TEST LOG).
    /// </summary>
    private void SetStatus(string text, Color color, string? hint = null)
    {
        if (_status.IsDisposed) return;
        if (_status.InvokeRequired) { _status.BeginInvoke(new Action(() => SetStatus(text, color, hint))); return; }
        if (_retainTestResult && !_running) return;
        _status.Text = VietnameseText.Status(text);
        FitStatusFont();
        _status.BackColor = color;
        _status.Invalidate();
        if (hint != null) _lblStartHint.Text = hint;
        _retainTestResult = text is "PASS" or "FAIL" or "ERROR" or "STOP" or "MACHINE ERROR";
    }

    /// <summary>Số dòng tối đa giữ trong lưới lịch sử (phép thử cũ nhất bị đẩy khỏi lưới).</summary>
    private const int GridHistoryMax = 200;

    /// <summary>
    /// Nạp lịch sử kết quả của ca hiện tại vào lưới (từ báo cáo CSV):
    /// mỗi dòng = 1 phép thử, MỚI NHẤT Ở TRÊN CÙNG, tối đa <see cref="GridHistoryMax"/> dòng.
    /// Tiêu đề chỉ nhận diện bảng; toàn bộ giá trị nằm trong từng dòng kết quả.
    /// </summary>
    private void LoadGridHistory()
    {
        _lblTestItems.Text = "LỊCH SỬ KẾT QUẢ";
        var rows = _report.ReadRecentRows(_program.Name, DateTime.Now, GridHistoryMax);
        void apply()
        {
            _grid.Rows.Clear();
            // CSV lưu từ cũ -> mới; đảo ngược để dòng mới nhất nằm trên cùng.
            for (int i = rows.Count - 1; i >= 0; i--)
                AppendGridRow(rows[i]);
            HighlightLatestResult();
        }
        if (_grid.InvokeRequired) _grid.BeginInvoke((Action)apply); else apply();
    }

    /// <summary>Thêm 1 dòng kết quả MỚI lên đầu lưới (gọi sau mỗi phép thử xong).</summary>
    private void AddResultRow(HipotResult r)
    {
        var row = new ReportRow(_total, _program.Name, _program.VoltageKv, _program.FreqHz,
            _program.TimeSec, _program.RampSec, _program.RefMa, _program.HighSetMa,
            _program.LowSetMa, r.VoltageKv, r.CurrentMa, r.TimeSec, r.Result);
        void apply()
        {
            _grid.Rows.Insert(0, 1);
            FillGridRow(_grid.Rows[0], row);
            // Cũ nhất bị đẩy khỏi lưới khi vượt quá giới hạn (dữ liệu đầy đủ vẫn còn trong CSV).
            while (_grid.Rows.Count > GridHistoryMax)
                _grid.Rows.RemoveAt(_grid.Rows.Count - 1);
            HighlightLatestResult();
        }
        if (_grid.InvokeRequired) _grid.BeginInvoke((Action)apply); else apply();
    }

    /// <summary>Thêm 1 dòng vào CUỐI lưới (dùng khi nạp lịch sử).</summary>
    private void AppendGridRow(ReportRow row)
    {
        int idx = _grid.Rows.Add();
        FillGridRow(_grid.Rows[idx], row);
    }

    /// <summary>Nền xanh nhạt luôn theo dòng mới nhất, giữ màu chữ PASS/FAIL của từng dòng.</summary>
    private void HighlightLatestResult()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            bool latest = row.Index == 0;
            row.DefaultCellStyle.BackColor = latest ? ColorLatestResult : Color.White;
            row.DefaultCellStyle.SelectionBackColor = latest ? ColorLatestResult : Color.Empty;
            row.DefaultCellStyle.SelectionForeColor = latest ? row.DefaultCellStyle.ForeColor : Color.Empty;
        }
        _grid.ClearSelection();
        if (_grid.Rows.Count > 0) _grid.FirstDisplayedScrollingRowIndex = 0;
    }

    /// <summary>Điền dữ liệu 1 dòng theo đúng 12 cột của báo cáo Hipot_GPT9801.</summary>
    private void FillGridRow(DataGridViewRow row, ReportRow r)
    {
        string D(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] vals =
        {
            r.Name, D(r.VTestKv), D(r.FreqHz), D(r.TestSec), D(r.RampSec), D(r.RefMa),
            D(r.HiMa), D(r.LowMa), r.VResultKv.ToString("F3"), r.IResultMa.ToString("F1"),
            r.MeasureSec.ToString("F1"), VietnameseText.Result(r.Result)
        };
        for (int i = 0; i < vals.Length && i < row.Cells.Count; i++)
            row.Cells[i].Value = vals[i];
        bool pass = r.Result.Equals("PASS", StringComparison.OrdinalIgnoreCase);
        row.DefaultCellStyle.ForeColor = pass ? ColorPass : ColorFail;
    }

    #endregion

    #region Cài đặt (tab Setting) - cấu hình cổng serial, lưu vào Config\SystemSetting.xml trong bin

    /// <summary>
    /// Dựng giao diện tab Cài đặt - GỌN GÀN, chỉ còn phần cần thiết:
    ///   - Hai nhóm Cổng COM (Máy HIPOT + Bảng điều khiển Arduino), mỗi bên thêm nút Quét.
    ///     Baud / Parity / Data / Stop bits lấy từ file cấu hình (mặc định 115200 8N1),
    ///     không còn ô chỉnh trên UI để tránh đổi nhầm khiến mất kết nối.
    ///   - Nhóm Thao tác: Lưu / Áp dụng & Kết nối lại / Mở thư mục cấu hình.
    ///   - Nhóm Bảo mật: mật khẩu mở khóa cài đặt.
    /// Mọi giá trị đọc/ghi qua <see cref="_setting"/> - tức là file
    /// <c>Config\SystemSetting.xml</c> nằm trong thư mục bin cạnh file .exe.
    /// </summary>
    private void BuildSettingsTab()
    {
        _tabSettings.BackColor = Color.FromArgb(248, 250, 252);

        _grpHipot.Text = "MÁY THỬ CAO ÁP - GW Instek GPT-9801 (RS-232)";
        _grpHipot.Location = new Point(20, 20);
        _grpHipot.Size = new Size(690, 100);
        _grpHipot.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        BuildPortField(_grpHipot, _hipotPort, _btnScanHipot);

        _grpCb.Text = "BẢNG ĐIỀU KHIỂN - Arduino";
        _grpCb.Location = new Point(730, 20);
        _grpCb.Size = new Size(690, 100);
        _grpCb.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        BuildPortField(_grpCb, _cbPort, _btnScanCb);

        // ---------- Nhóm thao tác ----------
        _grpAction.Text = "Thao tác";
        _grpAction.Location = new Point(20, 140);
        _grpAction.Size = new Size(1400, 100);
        _grpAction.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        _btnSaveSettings.Text = "Lưu cấu hình";
        _btnSaveSettings.Location = new Point(20, 30);
        _btnSaveSettings.Size = new Size(180, 40);
        _btnSaveSettings.Click += (_, _) => SaveSettings();

        _btnApplySettings.Text = "Lưu và kết nối lại";
        _btnApplySettings.Location = new Point(215, 30);
        _btnApplySettings.Size = new Size(230, 40);
        _btnApplySettings.Click += async (_, _) => await ApplySettingsAndReconnectAsync();

        _btnOpenBin.Text = "Mở thư mục cấu hình";
        _btnOpenBin.Location = new Point(460, 30);
        _btnOpenBin.Size = new Size(200, 40);
        _btnOpenBin.Click += (_, _) => OpenFolder(_paths.ConfigDir, "thư mục cấu hình");

        // Thông báo kết quả thao tác (lưu / áp dụng) - chỉ hiện khi có sự kiện.
        _lblSetStatus.Location = new Point(680, 38);
        _lblSetStatus.Size = new Size(700, 25);
        _lblSetStatus.Text = "";
        _lblSetStatus.Font = new Font("Segoe UI", 9F);
        _lblSetStatus.ForeColor = Color.FromArgb(100, 116, 139);

        _grpAction.Controls.AddRange(new Control[]
        {
            _btnSaveSettings, _btnApplySettings, _btnOpenBin, _lblSetStatus
        });

        // ---------- Nhóm bảo mật ----------
        _grpSecurity.Text = "Bảo mật";
        _grpSecurity.Location = new Point(20, 260);
        _grpSecurity.Size = new Size(1400, 100);
        _grpSecurity.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        var lblPass = new Label
        {
            Text = "Mật khẩu cài đặt:",
            Location = new Point(20, 38),
            Size = new Size(150, 25),
            TextAlign = ContentAlignment.MiddleRight
        };
        _settingsPassword.Location = new Point(180, 36);
        _settingsPassword.Size = new Size(200, 25);
        _settingsPassword.UseSystemPasswordChar = true;
        _settingsPassword.Font = new Font("Segoe UI", 10F);

        _btnUnlockSettings.Text = "Mở khóa";
        _btnUnlockSettings.Location = new Point(400, 34);
        _btnUnlockSettings.Size = new Size(140, 30);
        _btnUnlockSettings.Click += (_, _) => ToggleSettingsLock();

        _grpSecurity.Controls.AddRange(new Control[]
        { lblPass, _settingsPassword, _btnUnlockSettings });

        _grpOfficeLicense.Text = "Tệp bản quyền office.dat";
        _grpOfficeLicense.Location = new Point(20, 380);
        _grpOfficeLicense.Size = new Size(1000, 125);
        _grpOfficeLicense.Visible = false;
        var officePath = new TextBox
        {
            ReadOnly = true, Text = OfficeLicenseService.LicensePath,
            Location = new Point(20, 30), Size = new Size(950, 25)
        };
        var openOfficeFolder = new Button
        {
            Text = "Mở thư mục office.dat", Location = new Point(20, 70), Size = new Size(220, 35)
        };
        openOfficeFolder.Click += (_, _) =>
        {
            if (_settingsAccess.IsRoot)
                OpenFolder(OfficeLicenseService.LicenseDirectory, "thư mục office.dat", logPath: false);
        };
        _grpOfficeLicense.Controls.AddRange(new Control[] { officePath, openOfficeFolder });

        _tabSettings.Controls.AddRange(new Control[]
        { _grpHipot, _grpCb, _grpAction, _grpSecurity, _grpOfficeLicense });
        BuildTimingSettings();
        ArrangeSettingsLayout();
    }

    /// <summary>
    /// Dựng 1 nhóm cấu hình cổng serial GỌN: 1 dòng "Cổng COM" + nút Quét.
    /// (Baud/Parity/Data/Stop bits lấy từ file cấu hình - không chỉnh trên UI.)
    /// </summary>
    private static void BuildPortField(GroupBox grp, ComboBox port, Button scan)
    {
        var lb = new Label
        {
            Text = "Cổng COM:",
            Location = new Point(20, 36),
            Size = new Size(110, 25),
            TextAlign = ContentAlignment.MiddleRight
        };
        port.Location = new Point(145, 33);
        port.Size = new Size(200, 27);
        port.DropDownStyle = ComboBoxStyle.DropDown;   // vừa chọn vừa gõ tay được (AUTO/COMx)
        port.Font = new Font("Consolas", 10F);
        port.Items.Add("AUTO");

        scan.Text = "Quét";
        scan.Location = new Point(360, 32);
        scan.Size = new Size(90, 30);

        grp.Controls.Add(lb);
        grp.Controls.Add(port);
        grp.Controls.Add(scan);
    }

    private void LoadSettingsToFields()
    {
        _valApplication.Text = Text;
        _valVersion.Text = _config.Get("Application", "Version", "1.0.0");

        SetCombo(_hipotPort, _setting.ComPortGPT);
        SetCombo(_cbPort, _setting.ComPortControl);
        LoadTimingInputs();

        _settingsPassword.Clear();
        _settingsAccess.Lock();
        _grpOfficeLicense.Visible = false;
        _btnUnlockSettings.Text = "Mở khóa";
        _lblSetStatus.Text = "";
        DisableSettings(true);
    }

    /// <summary>Đưa giá trị vào ComboBox, giữ nó trong danh sách gợi ý nếu chưa có.</summary>
    private static void SetCombo(ComboBox cb, string value)
    {
        if (value.Length == 0 || value.Equals("AUTO", StringComparison.OrdinalIgnoreCase)) value = "Tự động";
        if (value.Length > 0 && !cb.Items.Contains(value)) cb.Items.Add(value);
        cb.Text = value;
    }

    /// <summary>
    /// Ghi các ô đang hiển thị vào <c>_setting</c> rồi lưu ra
    /// <c>Config\SystemSetting.xml</c> trong thư mục bin.
    /// </summary>
    private bool SaveSettings(bool silent = false) => SaveSettingsCore(silent, applyToConnectedBoard: true);

    private bool SaveSettingsCore(bool silent, bool applyToConnectedBoard)
    {
        if (_connecting) return SettingsError("Đang kết nối thiết bị. Chờ kết nối hoàn tất rồi lưu cấu hình.", silent);
        if (!_settingsAccess.Unlocked) return true;
        if (_running || _fixtureStartPending || _returningToIdle)
            return SettingsError("Đang thực hiện chu trình. Chờ đồ gá về vị trí nghỉ rồi lưu cấu hình.", silent);
        CycleTimingSettings timing = ReadTimingInputs();
        if (!timing.TryValidate(out string timingError)) return SettingsError(timingError, silent);
        if (applyToConnectedBoard && _arduino.IsOpen && _fixtureMode)
        {
            var applied = ApplyBoardTimings(timing);
            if (!applied.Ready) return SettingsError(applied.Message, silent);
        }
        // Chỉ Cổng COM sửa được trên UI; Baud/Parity/Data/Stop giữ nguyên giá trị
        // đã đọc từ file cấu hình khi mở phần mềm (115200 8N1 theo mặc định).
        _setting.ComPortGPT = VietnameseText.PortCode(_hipotPort.Text);
        _setting.ComPortControl = VietnameseText.PortCode(_cbPort.Text);
        _setting.Timing = timing;

        try
        {
            _setting.Save(_paths.SettingFile);
            if (!_arduino.IsOpen || !applyToConnectedBoard)
            {
                _fixtureTimingReady = false;
                _timingStatus.Text = "Đã lưu trên máy tính; sẽ gửi thời gian khi kết nối bảng điều khiển.";
                _timingStatus.ForeColor = Color.DarkOrange;
            }
            LockSettings();
            if (!silent)
            {
                AddLog($"[CT] Đã lưu cấu hình cổng nối tiếp: {_paths.SettingFile}");
                AddLog($"[CT]   HIPOT : {Describe(_setting.ComPortGPT, _setting.ComBaudrateGPT, _setting.ComDataBitGPT, _setting.ComParityGPT, _setting.ComStopBitGPT)}");
                AddLog($"[CT]   BẢNG ĐIỀU KHIỂN : {Describe(_setting.ComPortControl, _setting.ComBaudrateControl, _setting.ComDataBitControl, _setting.ComParityControl, _setting.ComStopBitControl)}");
                _lblSetStatus.ForeColor = Color.ForestGreen;
                _lblSetStatus.Text = $"Đã lưu cấu hình lúc {DateTime.Now:HH:mm:ss}";
            }
            return true;
        }
        catch (Exception ex)
        {
            _fixtureTimingReady = false;
            AddLog($"[LỖI] Không lưu được file cấu hình: {ex.Message}");
            if (!silent)
                VietnameseMessageBox.Show($"Không lưu được tệp cấu hình:\n{_paths.SettingFile}\n\n{ex.Message}",
                                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static string Describe(string port, int baud, int dataBits, string parity, int stopBits)
    {
        char p = string.IsNullOrEmpty(parity) ? '?' : char.ToUpperInvariant(parity[0]);
        string portLabel = port.Equals("AUTO", StringComparison.OrdinalIgnoreCase) || port.Length == 0 ? "Tự động" : port;
        return $"{portLabel} @ {baud} {dataBits}{p}{stopBits}";
    }

    /// <summary>Nút "Áp dụng &amp; Kết nối lại": lưu file rồi dò/ghép nối lại cả hai thiết bị.</summary>
    private async Task ApplySettingsAndReconnectAsync()
    {
        if (_running)
        {
            VietnameseMessageBox.Show("Đang chạy phép thử - không đổi cấu hình được.", "Thông báo",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _btnApplySettings.Enabled = false;
        try { await ReconnectAsync(); }
        finally { DisableSettings(!_settingsAccess.Unlocked); }
    }

    /// <summary>Quét cổng serial đang có trên máy và đổ vào ô Cổng COM của cả hai nhóm.</summary>
    private void ScanSerialPorts()
    {
        string[] ports = HipotLink.GetPorts();
        foreach (ComboBox cb in new[] { _hipotPort, _cbPort })
        {
            string current = cb.Text;
            cb.Items.Clear();
            cb.Items.Add("Tự động");
            foreach (string p in ports) cb.Items.Add(p);
            SetCombo(cb, current);
        }
        AddLog(ports.Length == 0
            ? "[CT] Không thấy cổng nối tiếp nào trên máy."
            : "[CT] Đã quét cổng nối tiếp: " + string.Join(", ", ports));
    }

    /// <summary>Mở khóa / khóa lại các ô cấu hình bằng mật khẩu cài đặt.</summary>
    private void ToggleSettingsLock()
    {
        if (_settingsAccess.Unlocked && _settingsPassword.Text.Length == 0)
        {
            LockSettings();
            AddLog("[CT] Đã khóa lại cài đặt.");
            return;
        }

        if (!_settingsAccess.Unlock(_settingsPassword.Text))
        {
            VietnameseMessageBox.Show("Sai mật khẩu cài đặt.", "Không đủ quyền",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _btnUnlockSettings.Text = "Khóa lại";
        _settingsPassword.Clear();
        DisableSettings(false);
        _grpOfficeLicense.Visible = _settingsAccess.IsRoot;
        AddLog("[CT] Đã mở khóa cài đặt.");
    }

    private void DisableSettings(bool disabled)
    {
        disabled |= _connecting;
        foreach (var input in _timingInputs.Values) input.Enabled = !disabled;
        foreach (Control c in new Control[]
                 {
                     _hipotPort, _cbPort,
                     _btnScanHipot, _btnScanCb, _btnSaveSettings, _btnApplySettings
                 })
            c.Enabled = !disabled;
    }

    private void LockSettings()
    {
        _settingsAccess.Lock();
        _grpOfficeLicense.Visible = false;
        _btnUnlockSettings.Text = "Mở khóa";
        _settingsPassword.Clear();
        DisableSettings(true);
    }

    #endregion

    private void AddLog(string message)
    {
        if (_shuttingDown) return;
        if (!LogWriter.ShouldLog(message)) return;
        DateTimeOffset timestamp = DateTimeOffset.Now;
        // Ghi file TRƯỚC (thread-safe, không phụ thuộc UI) để không mất log nếu app bị đóng bất ngờ.
        // PHẢI tách phần hiển thị ra AppendToScreen, nếu không BeginInvoke sẽ gọi lại AddLog
        // trên UI thread và ghi dòng này vào file LẦN HAI.
        string? fileErr = _logFile.Write(message, timestamp);
        if (fileErr is not null && !_logFileWarningShown)
        {
            _logFileWarningShown = true;
            message += $"   <<{fileErr}; đóng Excel rồi bấm nút 'Mở thư mục nhật ký'>>";
        }

        if (_log is null || _log.IsDisposed) return;
        if (_log.InvokeRequired)
        {
            try { _log.BeginInvoke(new Action(() => AppendToScreen(message, timestamp))); }
            catch { /* form đang đóng */ }
            return;
        }
        AppendToScreen(message, timestamp);
    }

    /// <summary>Hiển thị một dòng log lên RichTextBox. Chỉ gọi trên UI thread.</summary>
    private void AppendToScreen(string message, DateTimeOffset timestamp)
    {
        if (_log.IsDisposed) return;
        Point scroll = _tabHome.AutoScrollPosition;
        string time = timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        _log.AppendText($"[{time}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
        // Cuộn nội dung log mà không kéo cả dashboard xuống theo con trỏ log.
        if (_tabHome.AutoScrollPosition != scroll)
            _tabHome.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
    }

    /// <summary>Mở thư mục chứa log + báo cáo + passrate trong Explorer.</summary>
    private void OpenLogFolder()
    {
        _logFile.Resume();                 // nếu vừa bị khóa bởi Excel thì cho ghi lại
        _logFileWarningShown = false;
        OpenFolder(_paths.LogDir, "thư mục nhật ký");
    }

    /// <summary>Mở một thư mục trong Explorer.</summary>
    private void OpenFolder(string folder, string label, bool logPath = true)
    {
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
            if (logPath) AddLog($"[CT] Đã mở {label}: {folder}");
        }
        catch (Exception ex)
        {
            if (logPath) AddLog($"[LỖI] Không mở được {label}: {ex.Message}");
            else VietnameseMessageBox.Show(this, $"Không mở được thư mục: {ex.Message}", "Cài đặt",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
