using System.Diagnostics;
using System.Drawing;
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
    private OfficeLicenseService _license;
    private readonly TextBox _officePath = new();
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
        _license = new OfficeLicenseService(_setting.OfficeDatPath);
        int dayStart = int.TryParse(_config.Get("Shift", "DayStartHour", "8"), out int ds) ? ds : 8;
        int nightStart = int.TryParse(_config.Get("Shift", "NightStartHour", "20"), out int ns) ? ns : 20;
        _report = new ReportWriter(_paths.ReportDir, _paths.PassrateDir, dayStart, nightStart);
        _logFile = new LogWriter(_paths.LogDir);

        Text = _config.Get("Application", "AppName", "GPT-9000 Safety Test System");
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1450, 900);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 250, 252);

        // Nút bấm - START/STOP bằng cách bấm vào khung TEST RESULT (_status.Click dưới đây),
        // không còn nút START/STOP + checkbox như giao diện cũ.
        StyleHeader();
        BuildGrid();
        BuildCharts();
        StyleLog();
        StyleStatus();

        // Cửa sổ hẹp (<= ~1320 px) chia 50/50 để khối TEST INFORMATION không bị cắt;
        // cửa sổ rộng giữ 62/38 cho lưới lịch sử kết quả rộng hơn.
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
                SaveSettings(silent: true);
                LockSettings();
            }
        };

        // Bắt sự kiện từ hai thiết bị
        _status.Click += (_, _) => { if (_running) RequestStop(); else StartTest(); };

        FormClosing += (_, _) => Shutdown();

        DisableSettings(true);
        LoadSettingsToFields();

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) =>
            _lblClock.Text = $"{DateTime.Now:yyyy/MM/dd}\r\n{DateTime.Now:HH:mm:ss}";
        _clockTimer.Start();
    }

    private void StyleHeader()
    {
        _headerPanel.BackColor = Color.FromArgb(30, 58, 95);
        _lblTitle.Text = "GPT-9000 Safety Test System";
        _lblTitle.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        _lblTitle.ForeColor = Color.White;
        _lblLogo.Text = "HAN";
        _lblLogo.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
        _lblLogo.ForeColor = Color.White;
        _lblLogo.BackColor = Color.FromArgb(0, 115, 170);
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
        _grid.Font = new Font("Segoe UI", 11F);
        _grid.RowTemplate.Height = 34;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(30, 58, 95),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleCenter
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
        foreach (string c in cols) _grid.Columns.Add(c, c);
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
    }

    private async void MainForm_Load(object? sender, EventArgs e)
        => await RunStartupChecksAsync();

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyHomeColumnSplit();
    }

    /// <summary>
    /// Chia lại 2 cột tab Home theo bề rộng cửa sổ: TEST INFORMATION đã dồn gọn
    /// sang phải nên cột trái (lịch sử kết quả + biểu đồ) được tối đa không gian.
    /// Cửa sổ >= 1420 px: 66/34; hẹp hơn: 56/44 (khối INFO ~470 px vẫn vừa).
    /// </summary>
    private void ApplyHomeColumnSplit()
    {
        if (_homeLayout is null || _homeLayout.ColumnStyles.Count < 2) return;

        float left = _homeLayout.Width < 1420 ? 56F : 66F;
        if (Math.Abs(_homeLayout.ColumnStyles[0].Width - left) < 0.1F) return;

        _homeLayout.ColumnStyles[0].Width = left;
        _homeLayout.ColumnStyles[1].Width = 100F - left;
        _homeLayout.PerformLayout();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5 && !_running) { StartTest(); return true; }
        if (keyData == Keys.Escape && _running) { RequestStop(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    #region Bước 2 - Kiểm tra kết nối GPT-9801 + Control Board

    /// <summary>Bước 2 - dò và kết nối cả hai thiết bị, đăng ký sự kiện, nạp chương trình.</summary>
    private async Task RunStartupChecksAsync()
    {
        _licenseAllowsTest = _license.CanStartTest();
        AddLog("[Bước 2] Đang kiểm tra kết nối GPT-9801 và Control Board...");

        LoadActiveProgram();

        bool hipotOk = await Task.Run(ConnectHipot);
        // Phải nạp tham số chương trình SAU khi đã mở cổng HIPOT.
        if (hipotOk) await Task.Run(ApplyProgramToHipot);
        bool cbOk = await Task.Run(ConnectControlBoard);

        HookDeviceEvents(cbOk);

        if (!hipotOk || !cbOk)
        {
            AddLog("[LỖI] Chưa kết nối đủ thiết bị - kiểm tra dây nối, driver và cổng COM.");
            AddLog("       HIPOT dùng Config\\SystemSetting.xml [ComPortGPT], Control Board dùng [ComPortControl].");
            SetStatus("NO DEV", ColorError, "Không kết nối đủ thiết bị. Kiểm tra kết nối rồi chạy lại.");
            return;
        }

        SetStatus("IDLE", ColorIdle, "Fixture sẵn sàng. Đặt unit vào fixture để bắt đầu.");
    }

    /// <summary>Nút "Kết nối lại" - dò lại cả hai thiết bị mà không cần khởi động lại app.</summary>
    private async Task ReconnectAsync()
    {
        if (_running)
        {
            AddLog("[KẾT NỐI] Đang chạy phép thử - không dò lại được.");
            return;
        }
        SaveSettings();
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
        StartPolling();
    }

    /// <summary>Kết nối thật tới GPT-9801 qua RS-232, xác thực bằng *IDN?.</summary>
    private bool ConnectHipot()
    {
        string port = _setting.ComPortGPT.Trim();
        int baud = _setting.ComBaudrateGPT;
        AddLog($"[HIPOT] Đang kết nối {Describe(port, baud, _setting.ComDataBitGPT, _setting.ComParityGPT, _setting.ComStopBitGPT)}...");

        bool ok = port.Equals("AUTO", StringComparison.OrdinalIgnoreCase) || port.Length == 0
            ? _hipot.AutoConnect(baud, _setting.ParityGPT, _setting.ComDataBitGPT, _setting.StopBitsGPT,
                                 out string autoPort, out string err)
            : _hipot.Connect(port, baud, _setting.ParityGPT, _setting.ComDataBitGPT, _setting.StopBitsGPT, out err);

        _hipotOk = ok;
        if (ok)
        {
            AddLog($"[HIPOT] [OK] {_hipot.PortName} -> {_hipot.Idn}");
            SetLinkState(_valHipot, $"{_hipot.PortName} OK", ColorOk);
        }
        else
        {
            AddLog($"[HIPOT] [LỖI] {err}");
            AddLog("       Gợi ý: đổi ComPortGPT / baud / parity / data bit trong Config\\SystemSetting.xml.");
            AddLog("       Lưu ý bảng Arduino dùng cổng COM khác (ComPortControl) - đừng ghi lẫn.");
            SetLinkState(_valHipot, "NOT CONNECTED", ColorError);
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
    private bool ConnectControlBoard()
    {
        string port = _setting.ComPortControl.Trim();
        int baud = _setting.ComBaudrateControl;
        bool isAuto = port.Equals("AUTO", StringComparison.OrdinalIgnoreCase) || port.Length == 0;
        AddLog($"[CB] Đang kết nối {Describe(port, baud, _setting.ComDataBitControl, _setting.ComParityControl, _setting.ComStopBitControl)}...");

        string err;
        bool ok;
        if (isAuto)
        {
            ok = _arduino.AutoConnect(baud, out _, out err);
        }
        else
        {
            ok = _arduino.Connect(port, baud, out err);
            if (!ok)
            {
                AddLog($"[CB] Cổng {port} không phản hồi ({err}) - quét lại toàn bộ cổng serial...");
                ok = _arduino.AutoConnect(baud, out _, out err);
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
                    AddLog($"[CB] [OK] Đã ghim cổng {found} vào Config\\SystemSetting.xml " +
                           "- lần mở sau mở thẳng, không phải quét nữa.");
                }
                catch (Exception ex)
                {
                    AddLog($"[CB] [WARN] Không ghi được ghim cổng: {ex.Message}");
                }
            }
            AddLog($"[CB] [OK] {found} - {_arduino.BoardId}");
            AddLog($"[CB] [OK] Firmware: {_arduino.Firmware}");
            ArduinoLink.Reply st = _arduino.SendCommand("STATUS?", 1500);
            if (st.Ok) AddLog($"[CB] Trạng thái: {st.Text}");

            // Nhận diện chế độ fixture tự động từ STATUS? (firmware v1.2.0 có "FIX=..").
            // v1.3.0: STATUS? trả FIXTURE=/LOCK= (bản cũ là FIX=). Chấp nhận cả 3 để
            // phần mềm tự nhận diện chế độ fixture dù nâng cấp firmware từ bản cũ.
            _fixtureMode = (st.Ok && (st.Text.Contains("FIXTURE=") || st.Text.Contains("FIX="))) ||
                           (_arduino.Firmware?.Contains("FW=1.3") == true) ||
                           (_arduino.Firmware?.Contains("FW=1.2") == true);
            AddLog(_fixtureMode
                ? "[CB] [OK] CHẾ ĐỘ FIXTURE TỰ ĐỘNG: START → xilanh đóng → pogo pin chạm unit → " +
                  "tự phát áp → kết quả → xilanh rút lên."
                : "[CB] [INFO] Chế độ thủ công (legacy): cảm biến nắp an toàn + cảm biến unit được kiểm " +
                  "tại thời điểm bấm START.");
            SetLinkState(_valCb, $"{found} OK", ColorOk);
        }
        else
        {
            _fixtureMode = false;
            AddLog($"[CB] [LỖI] {err}");
            if (isAuto)
                AddLog("       Gợi ý: ghi rõ ComPortControl (vd COM6) vào Config\\SystemSetting.xml " +
                       "để bỏ qua bước quét toàn bộ cổng serial.");
            SetLinkState(_valCb, "NOT CONNECTED", ColorError);
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
            if (!_running && !_licenseAllowsTest) return;
            AddLog("[CB] Nhận ABORT từ bảng điều khiển / nắp an toàn bị mở.");
            RequestStop(fromBoard: true);
        }
    }

    private void OnBoardEvent(string evt)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => OnBoardEvent(evt))); return; }
        if (!_running && !_licenseAllowsTest) return;
        AddLog($"[CB] {evt}");
        if (evt.Contains("INTERLOCK_OPEN", StringComparison.OrdinalIgnoreCase) ||
            evt.Contains("UNIT_LOST", StringComparison.OrdinalIgnoreCase) ||
            evt.Contains("RELAY_TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
            evt.Contains("STROKE_TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            // Chỉ đổi trạng thái khi đang chạy phép thử; khi idle mở nắp là bình thường.
            if (_running)
                SetStatus("STOP", ColorError, "An toàn kích hoạt - phép thử bị ngắt bởi Control Board.");
            AddLog(evt.Contains("STROKE_TIMEOUT", StringComparison.OrdinalIgnoreCase)
                ? "[CB] ⚠ XILANH KHÔNG ĐI HẾT HÀNH TRÌNH - đã cắt hết và rút về. " +
                  "Kiểm tra áp khí / van điện từ / xilanh bị kẹt TRƯỚC KHI thử tiếp."
                : "[CB] Điều kiện an toàn bị vi phạm - luôn kiểm tra lại trước START.");
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
            SetStatus("MACHINE ERROR", ColorError, "GPT-9801 báo InterLock - nắp an toàn chưa đóng.");
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
        "CLOSING"  => "FIXTURE ĐANG ĐÓNG (clamp đang khép lại)",
        "POGO_OUT" => "POGO PIN ĐANG ĐẨY RA (tiếp cận unit)",
        "ARMED"    => "ĐÃ KHÓA LIÊN ĐỘNG GPT-9800 - chờ PC cho phép phóng áp",
        "TESTING"  => "RELAY HV ĐÃ NGẮN - GPT-9800 đang phóng áp vào unit",
        "POGO_BACK" => "POGO PIN ĐANG RÚT VỀ",
        "FIX_OPEN" => "FIXTURE ĐANG MỞ RA",
        "PASS"     => "PASS - fixture đã mở",
        "FAIL"     => "FAIL - fixture đã mở",
        "ABORTED"  => "ĐÃ DỪNG KHẨN - fixture đang mở",
        "FAULT"    => "LỖI CƠ KHÍ - mọi van đã tắt, KHÔNG thử tiếp",
        "IDLE"     => "SẴN SÀNG - fixture mở, chờ unit kế",
        _ => s
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
            AddLog("[Bước 4] Chưa đạt: không cảm nhận unit trên fixture (UNIT != PRESENT).");
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
            AddLog($"[LỖI] Board KHÔNG cho phép phóng áp: {r.Raw}");
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
        if (_running) return;
        _licenseAllowsTest = _license.CanStartTest();
        if (!_licenseAllowsTest)
        {
            // A physical START may already have closed the fixture. Release it without
            // arming HV, changing results, displaying a dialog or recording a failed test.
            if (fromBoard && _arduino.IsOpen)
                _arduino.SendCommand("TEST ABORT", 800);
            return;
        }
        if (fromBoard) AddLog("[CB] Nhận START từ nút bấm trên bảng điều khiển.");

        if (!_hipotOk || !_hipot.IsOpen)
        {
            AddLog("[Bước 4] Không thể bắt đầu: chưa kết nối GPT-9801.");
            // Board có thể vừa vào ST_ARMED (nhận *TESTON#) - nhả ngay, KHÔNG để fixture
            // nằm kín + liên động khóa trong lúc dialog hiển thị và chờ ARM_WAIT_MS.
            if (_arduino.IsOpen)
            {
                ArduinoLink.Reply ab = _arduino.SendCommand("TEST ABORT", 800);
                if (ab.Ok) AddLog($"[CB] Đã nhả fixture do thiếu hipot: {ab.Text}");
            }
            DialogResult dr = MessageBox.Show(
                "Chưa kết nối GPT-9801. Kiểm tra cáp RS-232 và cổng COM.\n\n" +
                "Bấm Yes để dò lại kết nối ngay?",
                "Không đủ điều kiện test", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.Yes) await ReconnectAsync();
            return;
        }

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
                AddLog($"[LỖI] Board từ chối FIX START: {fx.Raw}");
                SetStatus("FAIL", ColorFail, "Không khởi động được chu trình fixture.");
                return;
            }
            return;                       // chu trình tự chạy tiếp khi nhận *TESTON#
        }
        else if (!CheckLegacyConditions())
        {
            // Chế độ thủ công: kiểm interlock + unit TRỰC TIẾP tại thời điểm START
            // (thay cho checkbox tick tay ở giao diện cũ).
            SetStatus("NOT READY", ColorIdle, "Chưa đạt điều kiện an toàn - xem TEST LOG.");
            return;
        }

        _running = true;
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
                    AddLog($"[Bước 4] Board TỪ CHỐI bắt đầu: {start.Raw}");
                    SetStatus("FAIL", ColorFail, "Board từ chối START: kiểm tra nắp an toàn / vị trí unit.");
                    await Task.Delay(1200);
                    return;
                }
            }

            // Giai đoạn khóa liên động: board đang ở ST_ARMED (fixture kín + pogo chạm +
            // DB-9 pin1-2 short) -> HV ARM mới đóng relay D4 cho GPT-9800 phóng áp.
            if (_fixtureMode && _arduino.IsOpen && !ArmHighVoltage())
            {
                SetStatus("FAIL", ColorFail, "Board từ chối mở đường phóng áp - kiểm tra liên động.");
                if (_hipot.IsOpen) _hipot.StopTest(out _);
                try { _arduino.SendCommand("TEST ABORT", 800); } catch { }
                await Task.Delay(1200);
                return;
            }

            if (!_hipot.IsOpen) throw new InvalidOperationException("Mất kết nối GPT-9801.");

            // 1) Bật phép thử trên máy
            string startErr = "";
            bool started = await Task.Run(() => _hipot.StartTest(out startErr));
            if (!started) throw new InvalidOperationException($"FUNC:TEST ON thất bại: {startErr}");

            // 2) Chờ máy chạy xong (ramp + test time) rồi đọc kết quả thật.
            //    Không đọc MEAS? trước khi đã đủ thời lượng ramp+test, vì query
            //    "FUNC:TEST ON;:FUNC:TEST?" có thể trả "OK" ngay khi máy nhận lệnh.
            int minMs = (int)((_program.RampSec + _program.TimeSec) * 1000) + 1500;
            string? doneLine = await WaitHipotAsync(
                l => l.Contains("OK", StringComparison.OrdinalIgnoreCase),
                _program.EstimatedCycleMs, minMs);
            if (_stopRequested) { AddLog("[DỪNG] Phép thử bị dừng trước khi đọc kết quả."); return; }
            if (doneLine is null) throw new TimeoutException("GPT-9801 không báo hoàn tất (timeout).");

            HipotResult? res = await Task.Run(() => _hipot.ReadMeasurement(4000));
            if (res is null) throw new TimeoutException("Không đọc được MEAS? từ GPT-9801.");

            _cycleWatch.Stop();

            bool pass = res.IsPass && !res.IsError;
            SetStatus(pass ? "PASS" : "FAIL", pass ? ColorPass : ColorFail,
                      pass ? "Phép thử đạt." : $"Phép thử không đạt ({res.Result}).");
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
    private void RequestStop(bool fromBoard = false)
    {
        if (!_running) { AddLog("[DỪNG] Không có phép thử nào đang chạy."); return; }
        _stopRequested = true;
        AddLog($"[DỪNG] Yêu cầu dừng phép thử{(fromBoard ? " từ bảng điều khiển" : " bởi người vận hành")}...");
        if (_hipot.IsOpen) _hipot.StopTest(out _);

        // Không echo lệnh ABORT về cho board khi CHÍNH board là người khởi xướng.
        // Board đã tự cắt relay + tự rút xilanh rồi mới gửi *CANCEL#; gửi TEST ABORT
        // lần nữa sẽ chạy requestAbort() lần 2 -> retract() gọi lại và RESET strokeSinceMs,
        // làm thời gian rút bị cộng dồn vô hạn. Cùng mô-típ với lỗi gửi TEST START sau *TESTON#.
        if (_arduino.IsOpen && !fromBoard) _arduino.SendCommand("TEST ABORT", 800);
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
        AddLog($"[TỔNG] Cycle {cycleSeconds:F2} s | Pass {_pass} | Fail {_fail} | Total {_total}");
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
        // KHÔNG gửi "RESET" ở đây. Lệnh *PASS# / *FAIL# đã làm board:
        //   - tắt relay, bật đèn kết quả
        //   - và BẮT ĐẦU rút xilanh (solUpOn)
        // RESET gửi ngay sau đó sẽ gọi solBothOff() + lampsOff() -> dừng xilanh
        // giữa chừng và tắt luôn đèn vừa bật, khi operator còn chưa thấy kết quả.
        // Board tự về ST_IDLE sau khi rút pogo (POGO_BACK_MS) rồi mở fixture (FIX_OPEN_MS).
        await Task.Delay(300);

        if (_fixtureMode && _arduino.IsOpen)
        {
            // Chờ board rút xilanh xong (STATE=IDLE) rồi mới báo sẵn sàng: bấm START
            // giữa chừng sẽ nhận "ERR FIX_NOT_IDLE" từ board.
            bool idleNow = false;
            for (int i = 0; i < 80; i++)              // tối đa ~8 s (POGO_BACK 1,2 s + FIX_OPEN 2,5 s + dự phòng)
            {
                await Task.Delay(100);
                ArduinoLink.Reply fx = _arduino.SendCommand("FIX STATUS", 250);
                if (fx.Ok && ArduinoLink.GetField(fx.Text, "STATE")
                        .Equals("IDLE", StringComparison.OrdinalIgnoreCase))
                {
                    idleNow = true;
                    break;
                }
            }
            if (!idleNow)
                AddLog("[WARN] Xilanh chưa về vị trí trên - chờ thêm trước khi START unit kế.");
        }

        SetStatus("IDLE", ColorIdle, _hipotOk
            ? (_fixtureMode
                ? "Đặt unit mới vào fixture rồi bấm START để chu trình tự chạy."
                : "Fixture sẵn sàng. Đặt unit mới vào fixture để bắt đầu.")
            : "Mất kết nối GPT-9801. Kiểm tra thiết bị.");
    }

    private void Shutdown()
    {
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
        if (_running) { MessageBox.Show("Đang chạy phép thử - không đổi chương trình.", "Thông báo",
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
        _status.Text = text;
        _status.BackColor = color;
        _status.Invalidate();
        if (hint != null) _lblStartHint.Text = hint;
    }

    /// <summary>Số dòng tối đa giữ trong lưới lịch sử (phép thử cũ nhất bị đẩy khỏi lưới).</summary>
    private const int GridHistoryMax = 200;

    /// <summary>
    /// Nạp lịch sử kết quả của ca hiện tại vào lưới (từ báo cáo CSV):
    /// mỗi dòng = 1 phép thử, MỚI NHẤT Ở TRÊN CÙNG, tối đa <see cref="GridHistoryMax"/> dòng.
    /// Đồng thời hiển thị tóm tắt tham số chương trình trên tiêu đề lưới.
    /// </summary>
    private void LoadGridHistory()
    {
        _lblTestItems.Text =
            $"TEST RESULTS - {_programFile} | {_program}" +
            $" | Hi {_program.HighSetMa} mA / Low {_program.LowSetMa} mA   (mới nhất ở trên cùng)";
        var rows = _report.ReadRecentRows(_program.Name, DateTime.Now, GridHistoryMax);
        void apply()
        {
            _grid.Rows.Clear();
            // CSV lưu từ cũ -> mới; đảo ngược để dòng mới nhất nằm trên cùng.
            for (int i = rows.Count - 1; i >= 0; i--)
                AppendGridRow(rows[i]);
            if (_grid.Rows.Count > 0) _grid.FirstDisplayedScrollingRowIndex = 0;
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
        }
        if (_grid.InvokeRequired) _grid.BeginInvoke((Action)apply); else apply();
    }

    /// <summary>Thêm 1 dòng vào CUỐI lưới (dùng khi nạp lịch sử).</summary>
    private void AppendGridRow(ReportRow row)
    {
        int idx = _grid.Rows.Add();
        FillGridRow(_grid.Rows[idx], row);
    }

    /// <summary>Điền dữ liệu 1 dòng theo đúng 12 cột của báo cáo Hipot_GPT9801.</summary>
    private void FillGridRow(DataGridViewRow row, ReportRow r)
    {
        string D(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] vals =
        {
            r.Name, D(r.VTestKv), D(r.FreqHz), D(r.TestSec), D(r.RampSec), D(r.RefMa),
            D(r.HiMa), D(r.LowMa), r.VResultKv.ToString("F3"), r.IResultMa.ToString("F1"),
            r.MeasureSec.ToString("F1"), r.Result
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

        _grpHipot.Text = "MÁY HIPOT - GW Instek GPT-9801 (RS-232)";
        _grpHipot.Location = new Point(20, 20);
        _grpHipot.Size = new Size(690, 100);
        _grpHipot.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        BuildPortField(_grpHipot, _hipotPort, _btnScanHipot);

        _grpCb.Text = "BẢNG ĐIỀU KHIỂN - Arduino Control Board";
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

        _btnApplySettings.Text = "Áp dụng & Kết nối lại";
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

        _grpOfficeLicense.Text = "office.dat";
        _grpOfficeLicense.Location = new Point(20, 380);
        _grpOfficeLicense.Size = new Size(1000, 125);
        _grpOfficeLicense.Visible = false;
        _officePath.ReadOnly = true;
        _officePath.Text = string.IsNullOrWhiteSpace(_setting.OfficeDatPath)
            ? OfficeLicenseService.LicensePath : _setting.OfficeDatPath;
        _officePath.Location = new Point(20, 30);
        _officePath.Size = new Size(950, 25);
        var openOfficeFolder = new Button
        {
            Text = "Chọn file office.dat...", Location = new Point(20, 70), Size = new Size(220, 35)
        };
        openOfficeFolder.Click += (_, _) =>
        {
            if (!_settingsAccess.IsRoot) return;
            using var picker = new OpenFileDialog
            {
                Title = "Chọn file office.dat",
                Filter = "File office.dat (office.dat)|office.dat",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true,
                FileName = "office.dat"
            };
            string? folder = Path.GetDirectoryName(_officePath.Text);
            if (Directory.Exists(folder)) picker.InitialDirectory = folder;
            if (picker.ShowDialog(this) == DialogResult.OK)
                _officePath.Text = picker.FileName;
        };
        _grpOfficeLicense.Controls.AddRange(new Control[] { _officePath, openOfficeFolder });

        _tabSettings.Controls.AddRange(new Control[]
        { _grpHipot, _grpCb, _grpAction, _grpSecurity, _grpOfficeLicense });
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
        _valApplication.Text = _config.Get("Application", "AppName", "GPT-9000 Safety Test System");
        _valVersion.Text = _config.Get("Application", "Version", "1.0.0");

        SetCombo(_hipotPort, _setting.ComPortGPT);
        SetCombo(_cbPort, _setting.ComPortControl);

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
        if (value.Length > 0 && !cb.Items.Contains(value)) cb.Items.Add(value);
        cb.Text = value;
    }

    /// <summary>
    /// Ghi các ô đang hiển thị vào <c>_setting</c> rồi lưu ra
    /// <c>Config\SystemSetting.xml</c> trong thư mục bin.
    /// </summary>
    private bool SaveSettings(bool silent = false)
    {
        if (!_settingsAccess.Unlocked) return true;
        // Chỉ Cổng COM sửa được trên UI; Baud/Parity/Data/Stop giữ nguyên giá trị
        // đã đọc từ file cấu hình khi mở phần mềm (115200 8N1 theo mặc định).
        _setting.ComPortGPT = _hipotPort.Text.Trim();
        _setting.ComPortControl = _cbPort.Text.Trim();
        string previousOfficePath = _setting.OfficeDatPath;
        if (_settingsAccess.IsRoot) _setting.OfficeDatPath = _officePath.Text;

        try
        {
            _setting.Save(_paths.SettingFile);
            _license = new OfficeLicenseService(_setting.OfficeDatPath);
            LockSettings();
            if (!silent)
            {
                AddLog($"[CT] Đã lưu cấu hình cổng serial: {_paths.SettingFile}");
                AddLog($"[CT]   HIPOT : {Describe(_setting.ComPortGPT, _setting.ComBaudrateGPT, _setting.ComDataBitGPT, _setting.ComParityGPT, _setting.ComStopBitGPT)}");
                AddLog($"[CT]   BOARD : {Describe(_setting.ComPortControl, _setting.ComBaudrateControl, _setting.ComDataBitControl, _setting.ComParityControl, _setting.ComStopBitControl)}");
                _lblSetStatus.Text = $"Đã lưu cấu hình lúc {DateTime.Now:HH:mm:ss}";
            }
            return true;
        }
        catch (Exception ex)
        {
            _setting.OfficeDatPath = previousOfficePath;
            AddLog($"[LỖI] Không lưu được file cấu hình: {ex.Message}");
            if (!silent)
                MessageBox.Show($"Không lưu được file cấu hình:\n{_paths.SettingFile}\n\n{ex.Message}",
                                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static string Describe(string port, int baud, int dataBits, string parity, int stopBits)
    {
        char p = string.IsNullOrEmpty(parity) ? '?' : char.ToUpperInvariant(parity[0]);
        return $"{port} @ {baud} {dataBits}{p}{stopBits}";
    }

    /// <summary>Nút "Áp dụng &amp; Kết nối lại": lưu file rồi dò/ghép nối lại cả hai thiết bị.</summary>
    private async Task ApplySettingsAndReconnectAsync()
    {
        if (_running)
        {
            MessageBox.Show("Đang chạy phép thử - không đổi cấu hình được.", "Thông báo",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!SaveSettings()) return;
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
            cb.Items.Add("AUTO");
            foreach (string p in ports) cb.Items.Add(p);
            cb.Text = current.Length > 0 ? current : "AUTO";
        }
        AddLog(ports.Length == 0
            ? "[CT] Không thấy cổng serial nào trên máy."
            : "[CT] Đã quét cổng serial: " + string.Join(", ", ports));
    }

    /// <summary>Mở khóa / khóa lại các ô cấu hình bằng mật khẩu cài đặt.</summary>
    private void ToggleSettingsLock()
    {
        if (_settingsAccess.Unlocked && _settingsPassword.Text.Length == 0)
        {
            LockSettings();
            AddLog("[CT] Đã khóa lại ô cài đặt cổng serial.");
            return;
        }

        if (!_settingsAccess.Unlock(_settingsPassword.Text))
        {
            MessageBox.Show("Sai mật khẩu cài đặt.", "Không đủ quyền",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _btnUnlockSettings.Text = "Khóa lại";
        _settingsPassword.Clear();
        DisableSettings(false);
        _grpOfficeLicense.Visible = _settingsAccess.IsRoot;
        AddLog("[CT] Đã mở khóa ô cài đặt cổng serial.");
    }

    private void DisableSettings(bool disabled)
    {
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
        _officePath.Text = string.IsNullOrWhiteSpace(_setting.OfficeDatPath)
            ? OfficeLicenseService.LicensePath : _setting.OfficeDatPath;
        _grpOfficeLicense.Visible = false;
        _btnUnlockSettings.Text = "Mở khóa";
        _settingsPassword.Clear();
        DisableSettings(true);
    }

    #endregion

    private void AddLog(string message)
    {
        // Ghi file TRƯỚC (thread-safe, không phụ thuộc UI) để không mất log nếu app bị đóng bất ngờ.
        // PHẢI tách phần hiển thị ra AppendToScreen, nếu không BeginInvoke sẽ gọi lại AddLog
        // trên UI thread và ghi dòng này vào file LẦN HAI.
        string? fileErr = _logFile.Write(message);
        if (fileErr is not null && !_logFileWarningShown)
        {
            _logFileWarningShown = true;
            message += $"   <<{fileErr}; đóng Excel rồi bấm nút 'Mở thư mục log'>>";
        }

        if (_log is null || _log.IsDisposed) return;
        if (_log.InvokeRequired)
        {
            try { _log.BeginInvoke(new Action(() => AppendToScreen(message))); }
            catch { /* form đang đóng */ }
            return;
        }
        AppendToScreen(message);
    }

    /// <summary>Hiển thị một dòng log lên RichTextBox. Chỉ gọi trên UI thread.</summary>
    private void AppendToScreen(string message)
    {
        if (_log.IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    /// <summary>Mở thư mục chứa log + báo cáo + passrate trong Explorer.</summary>
    private void OpenLogFolder()
    {
        _logFile.Resume();                 // nếu vừa bị khóa bởi Excel thì cho ghi lại
        _logFileWarningShown = false;
        OpenFolder(_paths.LogDir, "thư mục log");
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
            else MessageBox.Show(this, $"Không mở được thư mục: {ex.Message}", "Cài đặt",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
