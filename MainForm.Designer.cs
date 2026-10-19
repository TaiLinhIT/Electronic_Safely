namespace GPT9000_WinForms_TestSystem;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.TabControl _tabs;
    private System.Windows.Forms.TabPage _tabHome;
    private System.Windows.Forms.TabPage _tabSettings;
    private System.Windows.Forms.Panel _headerPanel;
    private System.Windows.Forms.Label _lblTitle;
    private System.Windows.Forms.Label _lblLogo;
    private System.Windows.Forms.TableLayoutPanel _homeLayout;
    private System.Windows.Forms.GroupBox _grpTestItems;
    private System.Windows.Forms.Label _lblTestItems;
    private System.Windows.Forms.Panel _gridPanel;
    private System.Windows.Forms.GroupBox _grpTestInfo;
    private System.Windows.Forms.Label _lblTestInfo;
    private System.Windows.Forms.Label _lblApplication;
    private System.Windows.Forms.Label _valApplication;
    private System.Windows.Forms.Label _lblVersion;
    private System.Windows.Forms.Label _valVersion;
    private System.Windows.Forms.Label _lblCycleTime;
    private System.Windows.Forms.Label _valCycleTime;
    private System.Windows.Forms.Label _lblPass;
    private System.Windows.Forms.Label _valPass;
    private System.Windows.Forms.Label _lblFail;
    private System.Windows.Forms.Label _valFail;
    private System.Windows.Forms.Label _lblFpy;
    private System.Windows.Forms.Label _valFpy;
    private System.Windows.Forms.Label _lblTotal;
    private System.Windows.Forms.Label _valTotal;
    private System.Windows.Forms.GroupBox _grpTestLog;
    private System.Windows.Forms.Label _lblTestLog;
    private System.Windows.Forms.Panel _logPanel;
    private System.Windows.Forms.GroupBox _grpTestResult;
    private System.Windows.Forms.Label _lblTestResult;
    private System.Windows.Forms.Label _status;
    private System.Windows.Forms.CheckBox _chkUnitReady;
    private System.Windows.Forms.Button _btnStartTest;
    private System.Windows.Forms.Label _lblStepHint;
    // Controls của tab Setting (COM port HIPOT + Control Board) được dựng trong code,
    // xem MainForm.BuildSettingsTab(). Ở đây chỉ giữ lại TabPage.

    private System.Windows.Forms.Label _lblModel;
    private System.Windows.Forms.Label _valModel;
    private System.Windows.Forms.Label _lblProgram;
    private System.Windows.Forms.Label _valProgram;
    private System.Windows.Forms.Label _lblHipot;
    private System.Windows.Forms.Label _valHipot;
    private System.Windows.Forms.Label _lblCb;
    private System.Windows.Forms.Label _valCb;
    private System.Windows.Forms.Label _lblClock;
    private System.Windows.Forms.Button _btnSelectProgram;
    private System.Windows.Forms.Button _btnEditProgram;
    private System.Windows.Forms.Button _btnLogin;
    private System.Windows.Forms.Button _btnStop;
    private System.Windows.Forms.Button _btnReconnect;
    private System.Windows.Forms.Button _btnOpenLog;

    // ---- Controls của tab Cài đặt (dựng trong BuildSettingsTab) ----
    private System.Windows.Forms.GroupBox _grpHipot = new();
    private System.Windows.Forms.GroupBox _grpCb = new();
    private System.Windows.Forms.GroupBox _grpAction = new();
    private System.Windows.Forms.GroupBox _grpSecurity = new();
    private System.Windows.Forms.ComboBox _hipotPort = new();
    private System.Windows.Forms.ComboBox _hipotBaud = new();
    private System.Windows.Forms.ComboBox _hipotParity = new();
    private System.Windows.Forms.ComboBox _hipotDataBits = new();
    private System.Windows.Forms.ComboBox _hipotStopBits = new();
    private System.Windows.Forms.ComboBox _cbPort = new();
    private System.Windows.Forms.ComboBox _cbBaud = new();
    private System.Windows.Forms.ComboBox _cbParity = new();
    private System.Windows.Forms.ComboBox _cbDataBits = new();
    private System.Windows.Forms.ComboBox _cbStopBits = new();
    private System.Windows.Forms.Button _btnScanHipot = new();
    private System.Windows.Forms.Button _btnScanCb = new();
    private System.Windows.Forms.Button _btnSaveSettings = new();
    private System.Windows.Forms.Button _btnApplySettings = new();
    private System.Windows.Forms.Button _btnOpenBin = new();
    private System.Windows.Forms.TextBox _settingsPassword = new();
    private System.Windows.Forms.Button _btnUnlockSettings = new();
    private System.Windows.Forms.Label _lblConfigPath = new();
    private System.Windows.Forms.Label _lblSetStatus = new();

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this._tabs = new System.Windows.Forms.TabControl();
        this._tabHome = new System.Windows.Forms.TabPage();
        this._tabSettings = new System.Windows.Forms.TabPage();
        this._headerPanel = new System.Windows.Forms.Panel();
        this._lblTitle = new System.Windows.Forms.Label();
        this._lblLogo = new System.Windows.Forms.Label();
        this._homeLayout = new System.Windows.Forms.TableLayoutPanel();
        this._grpTestItems = new System.Windows.Forms.GroupBox();
        this._lblTestItems = new System.Windows.Forms.Label();
        this._gridPanel = new System.Windows.Forms.Panel();
        this._grpTestInfo = new System.Windows.Forms.GroupBox();
        this._lblTestInfo = new System.Windows.Forms.Label();
        this._lblApplication = new System.Windows.Forms.Label();
        this._valApplication = new System.Windows.Forms.Label();
        this._lblVersion = new System.Windows.Forms.Label();
        this._valVersion = new System.Windows.Forms.Label();
        this._lblCycleTime = new System.Windows.Forms.Label();
        this._valCycleTime = new System.Windows.Forms.Label();
        this._lblPass = new System.Windows.Forms.Label();
        this._valPass = new System.Windows.Forms.Label();
        this._lblFail = new System.Windows.Forms.Label();
        this._valFail = new System.Windows.Forms.Label();
        this._lblFpy = new System.Windows.Forms.Label();
        this._valFpy = new System.Windows.Forms.Label();
        this._lblTotal = new System.Windows.Forms.Label();
        this._valTotal = new System.Windows.Forms.Label();
        this._grpTestLog = new System.Windows.Forms.GroupBox();
        this._lblTestLog = new System.Windows.Forms.Label();
        this._logPanel = new System.Windows.Forms.Panel();
        this._grpTestResult = new System.Windows.Forms.GroupBox();
        this._lblTestResult = new System.Windows.Forms.Label();
        this._status = new System.Windows.Forms.Label();
        this._chkUnitReady = new System.Windows.Forms.CheckBox();
        this._btnStartTest = new System.Windows.Forms.Button();
        this._lblStepHint = new System.Windows.Forms.Label();
        this._lblModel = new System.Windows.Forms.Label();
        this._valModel = new System.Windows.Forms.Label();
        this._lblProgram = new System.Windows.Forms.Label();
        this._valProgram = new System.Windows.Forms.Label();
        this._lblHipot = new System.Windows.Forms.Label();
        this._valHipot = new System.Windows.Forms.Label();
        this._lblCb = new System.Windows.Forms.Label();
        this._valCb = new System.Windows.Forms.Label();
        this._lblClock = new System.Windows.Forms.Label();
        this._btnSelectProgram = new System.Windows.Forms.Button();
        this._btnEditProgram = new System.Windows.Forms.Button();
        this._btnLogin = new System.Windows.Forms.Button();
        this._btnStop = new System.Windows.Forms.Button();
        this._btnReconnect = new System.Windows.Forms.Button();
        this._btnOpenLog = new System.Windows.Forms.Button();

        this._tabs.SuspendLayout();
        this._tabHome.SuspendLayout();
        this._tabSettings.SuspendLayout();
        this._headerPanel.SuspendLayout();
        this._homeLayout.SuspendLayout();
        this._grpTestItems.SuspendLayout();
        this._grpTestInfo.SuspendLayout();
        this._grpTestLog.SuspendLayout();
        this._grpTestResult.SuspendLayout();
        this.SuspendLayout();

        // _headerPanel
        this._headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
        this._headerPanel.Controls.Add(this._lblTitle);
        this._headerPanel.Controls.Add(this._lblLogo);
        this._headerPanel.Location = new System.Drawing.Point(0, 0);
        this._headerPanel.Name = "_headerPanel";
        this._headerPanel.Size = new System.Drawing.Size(1450, 80);
        this._headerPanel.MinimumSize = new System.Drawing.Size(0, 60);
        this._headerPanel.MaximumSize = new System.Drawing.Size(0, 120);
        this._headerPanel.Padding = new System.Windows.Forms.Padding(10);

        // _lblTitle
        this._lblTitle.Font = new System.Drawing.Font("Segoe UI", 20F, System.Drawing.FontStyle.Bold);
        this._lblTitle.ForeColor = System.Drawing.Color.White;
        this._lblTitle.Location = new System.Drawing.Point(20, 20);
        this._lblTitle.Name = "_lblTitle";
        this._lblTitle.Size = new System.Drawing.Size(600, 40);
        this._lblTitle.Text = "GPT-9000 Safety Test System";

        // _lblLogo
        this._lblLogo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this._lblLogo.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Bold);
        this._lblLogo.ForeColor = System.Drawing.Color.White;
        this._lblLogo.Location = new System.Drawing.Point(1300, 15);
        this._lblLogo.Name = "_lblLogo";
        this._lblLogo.Size = new System.Drawing.Size(120, 50);
        this._lblLogo.Text = "HAN";
        this._lblLogo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this._lblLogo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        // _tabs
        this._tabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._tabs.Controls.Add(this._tabHome);
        this._tabs.Controls.Add(this._tabSettings);
        this._tabs.Location = new System.Drawing.Point(0, 80);
        this._tabs.Name = "_tabs";
        this._tabs.SelectedIndex = 0;
        this._tabs.Size = new System.Drawing.Size(1450, 820);
        this._tabs.Font = new System.Drawing.Font("Segoe UI", 9F);

        // _tabHome
        this._tabHome.AutoScroll = true;
        this._tabHome.Controls.Add(this._homeLayout);
        this._tabHome.Location = new System.Drawing.Point(4, 29);
        this._tabHome.Name = "_tabHome";
        this._tabHome.Padding = new System.Windows.Forms.Padding(10);
        this._tabHome.Size = new System.Drawing.Size(1442, 787);
        this._tabHome.Text = "Home";

        // _homeLayout
        this._homeLayout.ColumnCount = 2;
        this._homeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
        this._homeLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
        this._homeLayout.Controls.Add(this._grpTestItems, 0, 0);
        this._homeLayout.Controls.Add(this._grpTestInfo, 1, 0);
        this._homeLayout.Controls.Add(this._grpTestLog, 0, 1);
        this._homeLayout.Controls.Add(this._grpTestResult, 1, 1);
        this._homeLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._homeLayout.Location = new System.Drawing.Point(10, 10);
        this._homeLayout.Name = "_homeLayout";
        this._homeLayout.RowCount = 2;
        this._homeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this._homeLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
        this._homeLayout.Size = new System.Drawing.Size(1422, 767);

        // _grpTestItems
        this._grpTestItems.Controls.Add(this._lblTestItems);
        this._grpTestItems.Controls.Add(this._gridPanel);
        this._grpTestItems.Dock = System.Windows.Forms.DockStyle.Fill;
        this._grpTestItems.Location = new System.Drawing.Point(3, 3);
        this._grpTestItems.Name = "_grpTestItems";
        this._grpTestItems.Size = new System.Drawing.Size(846, 377);
        this._grpTestItems.Text = "";

        // _lblTestItems
        this._lblTestItems.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        this._lblTestItems.Location = new System.Drawing.Point(10, 5);
        this._lblTestItems.Name = "_lblTestItems";
        this._lblTestItems.Size = new System.Drawing.Size(200, 25);
        this._lblTestItems.Text = "TEST ITEMS";

        // _gridPanel
        this._gridPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._gridPanel.Location = new System.Drawing.Point(10, 35);
        this._gridPanel.Name = "_gridPanel";
        this._gridPanel.Size = new System.Drawing.Size(826, 330);
        this._gridPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;

        // _grpTestInfo
        this._grpTestInfo.Controls.Add(this._lblTestInfo);
        this._grpTestInfo.Controls.Add(this._lblApplication);
        this._grpTestInfo.Controls.Add(this._valApplication);
        this._grpTestInfo.Controls.Add(this._lblVersion);
        this._grpTestInfo.Controls.Add(this._valVersion);
        this._grpTestInfo.Controls.Add(this._lblCycleTime);
        this._grpTestInfo.Controls.Add(this._valCycleTime);
        this._grpTestInfo.Controls.Add(this._lblPass);
        this._grpTestInfo.Controls.Add(this._valPass);
        this._grpTestInfo.Controls.Add(this._lblFail);
        this._grpTestInfo.Controls.Add(this._valFail);
        this._grpTestInfo.Controls.Add(this._lblFpy);
        this._grpTestInfo.Controls.Add(this._valFpy);
        this._grpTestInfo.Controls.Add(this._lblTotal);
        this._grpTestInfo.Controls.Add(this._valTotal);
        this._grpTestInfo.Controls.Add(this._lblModel);
        this._grpTestInfo.Controls.Add(this._valModel);
        this._grpTestInfo.Controls.Add(this._lblProgram);
        this._grpTestInfo.Controls.Add(this._valProgram);
        this._grpTestInfo.Controls.Add(this._lblHipot);
        this._grpTestInfo.Controls.Add(this._valHipot);
        this._grpTestInfo.Controls.Add(this._lblCb);
        this._grpTestInfo.Controls.Add(this._valCb);
        this._grpTestInfo.Controls.Add(this._lblClock);
        this._grpTestInfo.Controls.Add(this._btnSelectProgram);
        this._grpTestInfo.Controls.Add(this._btnEditProgram);
        this._grpTestInfo.Controls.Add(this._btnLogin);
        this._grpTestInfo.Controls.Add(this._btnReconnect);
        this._grpTestInfo.Controls.Add(this._btnOpenLog);
        this._grpTestInfo.Dock = System.Windows.Forms.DockStyle.Fill;
        this._grpTestInfo.Location = new System.Drawing.Point(855, 3);
        this._grpTestInfo.Name = "_grpTestInfo";
        this._grpTestInfo.Size = new System.Drawing.Size(564, 377);
        this._grpTestInfo.Text = "";

        // _lblTestInfo
        this._lblTestInfo.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        this._lblTestInfo.Location = new System.Drawing.Point(10, 5);
        this._lblTestInfo.Name = "_lblTestInfo";
        this._lblTestInfo.Size = new System.Drawing.Size(200, 25);
        this._lblTestInfo.Text = "TEST INFORMATION";

        // _lblApplication
        this._lblApplication.Location = new System.Drawing.Point(10, 40);
        this._lblApplication.Name = "_lblApplication";
        this._lblApplication.Size = new System.Drawing.Size(120, 25);
        this._lblApplication.Text = "Application:";

        // _valApplication
        this._valApplication.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valApplication.Location = new System.Drawing.Point(140, 40);
        this._valApplication.Name = "_valApplication";
        this._valApplication.Size = new System.Drawing.Size(250, 25);
        this._valApplication.Text = "System";
        this._valApplication.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // _lblVersion
        this._lblVersion.Location = new System.Drawing.Point(10, 70);
        this._lblVersion.Name = "_lblVersion";
        this._lblVersion.Size = new System.Drawing.Size(120, 25);
        this._lblVersion.Text = "Version:";

        // _valVersion
        this._valVersion.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valVersion.Location = new System.Drawing.Point(140, 70);
        this._valVersion.Name = "_valVersion";
        this._valVersion.Size = new System.Drawing.Size(250, 25);
        this._valVersion.Text = "1.0.0";
        this._valVersion.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // _lblCycleTime
        this._lblCycleTime.Location = new System.Drawing.Point(10, 100);
        this._lblCycleTime.Name = "_lblCycleTime";
        this._lblCycleTime.Size = new System.Drawing.Size(120, 25);
        this._lblCycleTime.Text = "Cycle Time:";

        // _valCycleTime
        this._valCycleTime.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valCycleTime.Location = new System.Drawing.Point(140, 100);
        this._valCycleTime.Name = "_valCycleTime";
        this._valCycleTime.Size = new System.Drawing.Size(250, 25);
        this._valCycleTime.Text = "0.00 s";
        this._valCycleTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // _lblPass
        this._lblPass.Location = new System.Drawing.Point(10, 140);
        this._lblPass.Name = "_lblPass";
        this._lblPass.Size = new System.Drawing.Size(120, 25);
        this._lblPass.Text = "PASS";

        // _valPass
        this._valPass.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valPass.Location = new System.Drawing.Point(140, 140);
        this._valPass.Name = "_valPass";
        this._valPass.Size = new System.Drawing.Size(250, 25);
        this._valPass.Text = "0";
        this._valPass.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // _lblFail
        this._lblFail.Location = new System.Drawing.Point(10, 170);
        this._lblFail.Name = "_lblFail";
        this._lblFail.Size = new System.Drawing.Size(120, 25);
        this._lblFail.Text = "FAIL";

        // _valFail
        this._valFail.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valFail.Location = new System.Drawing.Point(140, 170);
        this._valFail.Name = "_valFail";
        this._valFail.Size = new System.Drawing.Size(250, 25);
        this._valFail.Text = "0";
        this._valFail.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // _lblFpy
        this._lblFpy.Location = new System.Drawing.Point(10, 200);
        this._lblFpy.Name = "_lblFpy";
        this._lblFpy.Size = new System.Drawing.Size(120, 25);
        this._lblFpy.Text = "FPY";

        // _valFpy
        this._valFpy.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valFpy.Location = new System.Drawing.Point(140, 200);
        this._valFpy.Name = "_valFpy";
        this._valFpy.Size = new System.Drawing.Size(250, 25);
        this._valFpy.Text = "0.00%";
        this._valFpy.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // _lblTotal
        this._lblTotal.Location = new System.Drawing.Point(10, 230);
        this._lblTotal.Name = "_lblTotal";
        this._lblTotal.Size = new System.Drawing.Size(120, 25);
        this._lblTotal.Text = "TOTAL";

        // _valTotal
        this._valTotal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valTotal.Location = new System.Drawing.Point(140, 230);
        this._valTotal.Name = "_valTotal";
        this._valTotal.Size = new System.Drawing.Size(250, 25);
        this._valTotal.Text = "0";
        this._valTotal.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // Model / Program info
        this._lblModel.Location = new System.Drawing.Point(10, 265);
        this._lblModel.Name = "_lblModel";
        this._lblModel.Size = new System.Drawing.Size(120, 25);
        this._lblModel.Text = "Model:";
        this._valModel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valModel.Location = new System.Drawing.Point(140, 265);
        this._valModel.Name = "_valModel";
        this._valModel.Size = new System.Drawing.Size(250, 25);
        this._valModel.Text = "-";
        this._valModel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        this._lblProgram.Location = new System.Drawing.Point(10, 295);
        this._lblProgram.Name = "_lblProgram";
        this._lblProgram.Size = new System.Drawing.Size(120, 25);
        this._lblProgram.Text = "Program:";
        this._valProgram.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valProgram.Location = new System.Drawing.Point(140, 295);
        this._valProgram.Name = "_valProgram";
        this._valProgram.Size = new System.Drawing.Size(250, 25);
        this._valProgram.Text = "-";
        this._valProgram.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        // Connection status
        this._lblHipot.Location = new System.Drawing.Point(410, 40);
        this._lblHipot.Name = "_lblHipot";
        this._lblHipot.Size = new System.Drawing.Size(140, 22);
        this._lblHipot.Text = "HIPOT (GPT-9800):";
        this._valHipot.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valHipot.ForeColor = System.Drawing.Color.Gray;
        this._valHipot.Location = new System.Drawing.Point(410, 62);
        this._valHipot.Name = "_valHipot";
        this._valHipot.Size = new System.Drawing.Size(140, 22);
        this._valHipot.Text = "NOT CONNECTED";

        this._lblCb.Location = new System.Drawing.Point(410, 95);
        this._lblCb.Name = "_lblCb";
        this._lblCb.Size = new System.Drawing.Size(140, 22);
        this._lblCb.Text = "CONTROL BOARD:";
        this._valCb.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this._valCb.ForeColor = System.Drawing.Color.Gray;
        this._valCb.Location = new System.Drawing.Point(410, 117);
        this._valCb.Name = "_valCb";
        this._valCb.Size = new System.Drawing.Size(140, 22);
        this._valCb.Text = "NOT CONNECTED";

        this._lblClock.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
        this._lblClock.ForeColor = System.Drawing.Color.FromArgb(30, 58, 95);
        this._lblClock.Location = new System.Drawing.Point(410, 150);
        this._lblClock.Name = "_lblClock";
        this._lblClock.Size = new System.Drawing.Size(145, 60);
        this._lblClock.Text = "0000/00/00\r\n00:00:00";
        this._lblClock.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this._lblClock.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._lblClock.BackColor = System.Drawing.Color.White;

        // Program management buttons
        this._btnSelectProgram.Location = new System.Drawing.Point(10, 330);
        this._btnSelectProgram.Name = "_btnSelectProgram";
        this._btnSelectProgram.Size = new System.Drawing.Size(130, 36);
        this._btnSelectProgram.TabIndex = 3;
        this._btnSelectProgram.Text = "Chọn CT";
        this._btnSelectProgram.UseVisualStyleBackColor = true;

        this._btnEditProgram.Location = new System.Drawing.Point(148, 330);
        this._btnEditProgram.Name = "_btnEditProgram";
        this._btnEditProgram.Size = new System.Drawing.Size(130, 36);
        this._btnEditProgram.TabIndex = 4;
        this._btnEditProgram.Text = "Sửa CT";
        this._btnEditProgram.UseVisualStyleBackColor = true;

        this._btnLogin.Location = new System.Drawing.Point(286, 330);
        this._btnLogin.Name = "_btnLogin";
        this._btnLogin.Size = new System.Drawing.Size(110, 36);
        this._btnLogin.TabIndex = 5;
        this._btnLogin.Text = "Đăng nhập";
        this._btnLogin.UseVisualStyleBackColor = true;

        // _btnReconnect - dò lại HIPOT + Control Board (dùng khi đổi cổng COM ở tab Setting)
        this._btnReconnect.Location = new System.Drawing.Point(410, 215);
        this._btnReconnect.Name = "_btnReconnect";
        this._btnReconnect.Size = new System.Drawing.Size(145, 40);
        this._btnReconnect.TabIndex = 7;
        this._btnReconnect.Text = "Kết nối lại";
        this._btnReconnect.UseVisualStyleBackColor = true;

        // _btnOpenLog - mở thư mục Log (cạnh file .exe) trong Explorer để lấy file log/báo cáo
        this._btnOpenLog.Location = new System.Drawing.Point(410, 262);
        this._btnOpenLog.Name = "_btnOpenLog";
        this._btnOpenLog.Size = new System.Drawing.Size(145, 40);
        this._btnOpenLog.TabIndex = 8;
        this._btnOpenLog.Text = "Mở thư mục log";
        this._btnOpenLog.UseVisualStyleBackColor = true;

        // _grpTestLog
        this._grpTestLog.Controls.Add(this._lblTestLog);
        this._grpTestLog.Controls.Add(this._logPanel);
        this._grpTestLog.Dock = System.Windows.Forms.DockStyle.Fill;
        this._grpTestLog.Location = new System.Drawing.Point(3, 390);
        this._grpTestLog.Name = "_grpTestLog";
        this._grpTestLog.Size = new System.Drawing.Size(846, 374);
        this._grpTestLog.Text = "";

        // _lblTestLog
        this._lblTestLog.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        this._lblTestLog.Location = new System.Drawing.Point(10, 5);
        this._lblTestLog.Name = "_lblTestLog";
        this._lblTestLog.Size = new System.Drawing.Size(200, 25);
        this._lblTestLog.Text = "TEST LOG";

        // _logPanel
        this._logPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._logPanel.Location = new System.Drawing.Point(10, 35);
        this._logPanel.Name = "_logPanel";
        this._logPanel.Size = new System.Drawing.Size(826, 330);
        this._logPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;

        // _grpTestResult
        this._grpTestResult.Controls.Add(this._lblTestResult);
        this._grpTestResult.Controls.Add(this._status);
        this._grpTestResult.Controls.Add(this._chkUnitReady);
        this._grpTestResult.Controls.Add(this._btnStartTest);
        this._grpTestResult.Controls.Add(this._btnStop);
        this._grpTestResult.Controls.Add(this._lblStepHint);
        this._grpTestResult.Dock = System.Windows.Forms.DockStyle.Fill;
        this._grpTestResult.Location = new System.Drawing.Point(855, 390);
        this._grpTestResult.Name = "_grpTestResult";
        this._grpTestResult.Size = new System.Drawing.Size(564, 374);
        this._grpTestResult.Text = "";

        // _lblTestResult
        this._lblTestResult.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        this._lblTestResult.Location = new System.Drawing.Point(10, 5);
        this._lblTestResult.Name = "_lblTestResult";
        this._lblTestResult.Size = new System.Drawing.Size(200, 25);
        this._lblTestResult.Text = "TEST RESULT";

        // _status
        this._status.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._status.Font = new System.Drawing.Font("Segoe UI", 40F, System.Drawing.FontStyle.Bold);
        this._status.Location = new System.Drawing.Point(10, 35);
        this._status.Name = "_status";
        this._status.Size = new System.Drawing.Size(544, 190);
        this._status.Text = "IDLE";
        this._status.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        this._status.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._status.Cursor = System.Windows.Forms.Cursors.Hand;

        // _chkUnitReady (Bước 3 - Đặt unit vào fixture)
        this._chkUnitReady.AutoSize = true;
        this._chkUnitReady.Location = new System.Drawing.Point(10, 232);
        this._chkUnitReady.Name = "_chkUnitReady";
        this._chkUnitReady.Size = new System.Drawing.Size(280, 19);
        this._chkUnitReady.TabIndex = 0;
        this._chkUnitReady.Text = "Unit đã vào fixture && nắp an toàn đã đóng";

        // _btnStartTest (Bước 4 - Nhận START TEST)
        this._btnStartTest.Location = new System.Drawing.Point(10, 260);
        this._btnStartTest.Name = "_btnStartTest";
        this._btnStartTest.Size = new System.Drawing.Size(140, 45);
        this._btnStartTest.TabIndex = 1;
        this._btnStartTest.Text = "START TEST";
        this._btnStartTest.UseVisualStyleBackColor = true;

        // _btnStop - dừng phép thử khẩn cấp
        this._btnStop.BackColor = System.Drawing.Color.Firebrick;
        this._btnStop.ForeColor = System.Drawing.Color.White;
        this._btnStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this._btnStop.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
        this._btnStop.Location = new System.Drawing.Point(155, 260);
        this._btnStop.Name = "_btnStop";
        this._btnStop.Size = new System.Drawing.Size(110, 45);
        this._btnStop.TabIndex = 6;
        this._btnStop.Text = "STOP";
        this._btnStop.UseVisualStyleBackColor = false;

        // _lblStepHint (Bước 8 - Trạng thái/ bước hiện tại)
        this._lblStepHint.Location = new System.Drawing.Point(275, 258);
        this._lblStepHint.Name = "_lblStepHint";
        this._lblStepHint.Size = new System.Drawing.Size(279, 90);
        this._lblStepHint.TabIndex = 2;
        this._lblStepHint.Text = "Bước 1/8 - Khởi động phần mềm";
        this._lblStepHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        // _tabSettings
        this._tabSettings.AutoScroll = true;
        this._tabSettings.Location = new System.Drawing.Point(4, 29);
        this._tabSettings.Name = "_tabSettings";
        this._tabSettings.Padding = new System.Windows.Forms.Padding(10);
        this._tabSettings.Size = new System.Drawing.Size(1442, 787);
        this._tabSettings.Text = "Cài đặt";

        // MainForm
        this.ClientSize = new System.Drawing.Size(1450, 900);
        this.Controls.Add(this._tabs);
        this.Controls.Add(this._headerPanel);
        this.Name = "MainForm";
        this.Text = "GPT-9000 Safety Test System";
        this.Load += new System.EventHandler(this.MainForm_Load);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
