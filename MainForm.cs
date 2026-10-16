using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GPT9000_WinForms_TestSystem;

public sealed class MainForm : Form
{
    private readonly IniConfig _config;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new();
    private readonly RichTextBox _log = new();
    private readonly Label _status = new();
    private readonly Label _passValue = new(), _failValue = new(), _totalValue = new(), _fpyValue = new(), _cycleValue = new();
    private readonly TextBox _fixture = new(), _server = new(), _database = new(), _dbUser = new(), _dbPassword = new(), _table = new();
    private readonly TextBox _dut = new(), _interface = new(), _settingsPassword = new();
    private readonly Button _revealPassword = new();
    private bool _passwordRevealed;
    private int _pass, _fail, _total;
    private readonly Stopwatch _cycleWatch = new();
    private bool _running;

    public MainForm()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
        _config = new IniConfig(configPath);
        Text = _config.Get("Application", "AppName", "GPT-9000 Safety Test System");
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1450, 900);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(242, 245, 249);

        // Subscribe to tab switching for auto-save
        _tabs.TabIndex = 0;
        _tabs.Selecting += Tabs_Selecting;

        BuildHomeTab();
        BuildSettingsTab();
        Controls.Add(_tabs);

        // Initially disable all settings fields
        DisableSettings(true);

        // Load settings and auto-apply
        LoadSettingsToFields();
        ApplyControlBoardSettings();

        AddLog("Application started. Demo mode is enabled; no high-voltage test has been performed.");
    }

    private void Tabs_Selecting(object? sender, TabControlCancelEventArgs e)
    {
        // Auto-save settings when switching tabs
        SaveSettings();
        AddLog("Settings auto-saved due to tab switch.");
    }

    private void BuildHomeTab()
    {
        var tab = new TabPage("Home") { BackColor = Color.FromArgb(242, 245, 249), Padding = new Padding(8) };
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(4) };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 43, 74), Padding = new Padding(18, 8, 18, 8) };
        var appName = new Label { Text = _config.Get("Application", "AppName", "GPT-9000 Safety Test System"), ForeColor = Color.White, Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, Location = new Point(8, 14) };
        var logo = new Label { Text = "HAN", ForeColor = Color.White, Font = new Font("Segoe UI", 22, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Right, Width = 115, BackColor = Color.FromArgb(0, 115, 170) };
        header.Controls.Add(appName); header.Controls.Add(logo);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        var testPanel = MakePanel("TEST ITEMS");
        var testContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true; _grid.RowHeadersVisible = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = Color.White; _grid.BorderStyle = BorderStyle.None;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(224, 233, 243);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 45, 60);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _grid.EnableHeadersVisualStyles = false;
        _grid.Columns.Add("Item", "Item"); _grid.Columns.Add("Value", "Value"); _grid.Columns.Add("LowLimit", "Low Limit");
        _grid.Columns.Add("UpLimit", "Up Limit"); _grid.Columns.Add("Result", "Result");
        _grid.Rows.Add("ACW Voltage", "—", "—", "—", "IDLE");
        _grid.Rows.Add("Leakage Current", "—", "—", "—", "IDLE");
        _grid.Rows.Add("Insulation Resistance", "—", "—", "—", "IDLE");
        _grid.Rows.Add("Ground Bond", "—", "—", "—", "IDLE");
        testContent.Controls.Add(_grid); testPanel.Controls.Add(testContent);
        var infoPanel = MakePanel("TEST INFORMATION");
        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(12) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58)); info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        AddInfoRow(info, 0, "Application", _config.Get("Application", "AppName", "GPT-9000 Safety Test System"));
        AddInfoRow(info, 1, "Version", _config.Get("Application", "Version", "1.0.0"));
        _cycleValue.Text = "0.00 s"; AddInfoRow(info, 2, "Cycle Time", _cycleValue);
        _passValue.Text = "0"; AddInfoRow(info, 3, "PASS", _passValue, Color.ForestGreen);
        _failValue.Text = "0"; AddInfoRow(info, 4, "FAIL", _failValue, Color.Firebrick);
        _fpyValue.Text = "0.00%"; AddInfoRow(info, 5, "FPY", _fpyValue);
        _totalValue.Text = "0"; AddInfoRow(info, 6, "TOTAL", _totalValue);
        infoPanel.Controls.Add(info);
        var logPanel = MakePanel("TEST LOG");
        _log.Dock = DockStyle.Fill; _log.ReadOnly = true; _log.BackColor = Color.FromArgb(18, 25, 38); _log.ForeColor = Color.Gainsboro;
        _log.Font = new Font("Consolas", 9.5F); _log.BorderStyle = BorderStyle.None;
        var logContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) }; logContent.Controls.Add(_log); logPanel.Controls.Add(logContent);
        var resultPanel = MakePanel("TEST RESULT");
        var resultContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        resultContent.RowStyles.Add(new RowStyle(SizeType.Percent, 68)); resultContent.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        _status.Text = "IDLE"; _status.Dock = DockStyle.Fill; _status.TextAlign = ContentAlignment.MiddleCenter;
        _status.Font = new Font("Segoe UI", 34, FontStyle.Bold); _status.ForeColor = Color.White; _status.BackColor = Color.FromArgb(110, 119, 129);
        // Click to start test (replaces buttons)
        _status.Click += (_, _) => StartTest();
        resultContent.Controls.Add(_status, 0, 0); resultPanel.Controls.Add(resultContent);
        body.Controls.Add(testPanel, 0, 0); body.Controls.Add(infoPanel, 1, 0);
        body.Controls.Add(logPanel, 0, 1); body.Controls.Add(resultPanel, 1, 1);
        page.Controls.Add(header, 0, 0); page.Controls.Add(body, 0, 1); tab.Controls.Add(page); _tabs.TabPages.Add(tab);
    }

    private void BuildSettingsTab()
    {
        var tab = new TabPage("Setting") { BackColor = Color.FromArgb(242, 245, 249), Padding = new Padding(18) };
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var title = new Label { Text = "SYSTEM SETTINGS", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.FromArgb(20, 43, 74), TextAlign = ContentAlignment.MiddleLeft };

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(20), Width = 800 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Fixture name
        AddSettingField(fields, "Fixture name", _fixture);

        // Database server name
        AddSettingField(fields, "Database server name", _server);

        // Database name
        AddSettingField(fields, "Database name", _database);

        // Database user
        AddSettingField(fields, "Database user", _dbUser);

        // Database password
        AddSettingField(fields, "Database password", _dbPassword);
        _dbPassword.UseSystemPasswordChar = true;

        // DUT setting (replaced COM port)
        AddSettingField(fields, "DUT", _dut);

        // Interface (RS232/USB) - kept but simplified
        AddSettingField(fields, "Interface (RS232/USB)", _interface);

        // Password field at bottom - dual purpose: show password and save
        var passwordRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _settingsPassword.Width = 180; _settingsPassword.UseSystemPasswordChar = true; _settingsPassword.PlaceholderText = "Enter settings password";
        _revealPassword.Text = "Show password"; _revealPassword.AutoSize = true;
        _revealPassword.Click += (_, _) => TogglePasswordReveal();
        passwordRow.Controls.Add(_settingsPassword); passwordRow.Controls.Add(_revealPassword);
        AddSettingField(fields, "Password", passwordRow);

        // ControlBoard section header
        var controlBoardTitle = new Label { Text = "CONTROLBOARD SETTINGS", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(20, 43, 74), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(10, 15, 0, 0) };
        var controlBoardPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(10) };
        var cbFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(20) };
        cbFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); cbFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // ControlBoard: Enable checkbox
        CheckBox cbEnabled = new CheckBox { Checked = _config.Get("ControlBoard", "Enabled", "true") == "true", AutoSize = true, Margin = new Padding(3, 3, 3, 5) };
        cbFields.Controls.Add(new Label { Text = "Enabled", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(55, 70, 88) }, 0, 0);
        cbFields.Controls.Add(cbEnabled, 1, 0);

        // ControlBoard: Port (read-only, shows AUTO)
        TextBox cbPort = new TextBox { Text = _config.Get("ControlBoard", "Port", "AUTO"), Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 5), ReadOnly = true, BackColor = Color.White, ForeColor = Color.Blue };
        cbFields.Controls.Add(new Label { Text = "Port", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(55, 70, 88) }, 0, 1);
        cbFields.Controls.Add(cbPort, 1, 1);

        // BaudRate field removed per requirements (auto-set)

        controlBoardPanel.Controls.Add(cbFields);

        // Buttons at bottom - only Reload (no Save needed - auto-save)
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var reload = MakeButton("RELOAD", Color.FromArgb(70, 92, 118)); reload.Click += (_, _) => LoadSettingsToFields();
        buttons.Controls.Add(reload);

        outer.Controls.Add(title, 0, 0);
        outer.Controls.Add(fields, 0, 1);
        outer.Controls.Add(controlBoardPanel, 0, 2);
        outer.Controls.Add(buttons, 0, 3);

        tab.Controls.Add(outer); _tabs.TabPages.Add(tab);
    }

    private static Panel MakePanel(string heading)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(0) };
        panel.Paint += (_, e) => { using var pen = new Pen(Color.FromArgb(218, 225, 234)); e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1); };
        var label = new Label { Text = heading, Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(35, 55, 78), BackColor = Color.FromArgb(248, 250, 252) };
        panel.Controls.Add(label); return panel;
    }

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9, FontStyle.Bold), Height = 38, Width = text.Length > 12 ? 145 : 100,
        Margin = new Padding(4), Cursor = Cursors.Hand
    };

    private static void AddInfoRow(TableLayoutPanel panel, int row, string name, string value, Color? valueColor = null)
    {
        var label = new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(95, 108, 123) };
        var val = new Label { Text = value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = valueColor ?? Color.FromArgb(30, 45, 60), Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoEllipsis = true };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7)); panel.Controls.Add(label, 0, row); panel.Controls.Add(val, 1, row);
    }
    private static void AddInfoRow(TableLayoutPanel panel, int row, string name, Label value, Color? valueColor = null)
    {
        value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleRight; value.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        if (valueColor.HasValue) value.ForeColor = valueColor.Value;
        var label = new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(95, 108, 123) };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7)); panel.Controls.Add(label, 0, row); panel.Controls.Add(value, 1, row);
    }

    private static void AddSettingField(TableLayoutPanel panel, string caption, Control control)
    {
        int row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var label = new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(55, 70, 88) };
        control.Dock = DockStyle.Fill; control.Margin = new Padding(3, 5, 3, 5);
        if (control is TextBox tb) { tb.BorderStyle = BorderStyle.FixedSingle; tb.Font = new Font("Segoe UI", 10); }
        panel.Controls.Add(label, 0, row); panel.Controls.Add(control, 1, row);
    }

    private void LoadSettingsToFields()
    {
        _fixture.Text = _config.Get("Fixture", "FixtureName", "FIXTURE-001");
        _server.Text = _config.Get("Database", "ServerName", "127.0.0.1");
        _database.Text = _config.Get("Database", "DatabaseName", "production_test");
        _dbUser.Text = _config.Get("Database", "User", "root");
        _dbPassword.Text = _config.Get("Database", "Password");
        _table.Text = _config.Get("Database", "Table", "test_results");
        // Model and baudrate removed per requirements
        _dut.Text = _config.Get("Device", "DUT", "GPT9800-Arduino");
        _interface.Text = _config.Get("Device", "Interface", "RS232");
        _settingsPassword.Clear(); _passwordRevealed = false; _dbPassword.UseSystemPasswordChar = true;
        _revealPassword.Text = "Show password";
    }

    private void SaveSettings()
    {
        _config.Set("Fixture", "FixtureName", _fixture.Text.Trim());
        _config.Set("Database", "ServerName", _server.Text.Trim());
        _config.Set("Database", "DatabaseName", _database.Text.Trim());
        _config.Set("Database", "User", _dbUser.Text.Trim());
        _config.Set("Database", "Password", _dbPassword.Text);
        _config.Set("Database", "Table", _table.Text.Trim());
        // Model and baudrate not saved per requirements
        _config.Set("Device", "DUT", _dut.Text.Trim());
        _config.Set("Device", "Interface", _interface.Text.Trim());
        _config.Save();
        // Silent auto-save - no MessageBox
    }

    private void ApplyControlBoardSettings()
    {
        // Apply control board settings from config - simulated
        string enabled = _config.Get("ControlBoard", "Enabled", "true");
        string port = _config.Get("ControlBoard", "Port", "AUTO");

        AddLog($"ControlBoard config: Enabled={enabled}, Port={port}");

        // In a real implementation, would initialize serial port here
        // for Arduino/GPT9800 communication based on settings
        if (enabled.Equals("true", StringComparison.OrdinalIgnoreCase) && port != "AUTO")
        {
            AddLog($"ControlBoard would initialize on port {port}");
        }
        else
        {
            AddLog("ControlBoard using simulated mode (port AUTO or disabled).");
        }
    }

    private void TogglePasswordReveal()
    {
        string expected = _config.Get("Security", "SettingsPassword", "123456");
        if (!_passwordRevealed)
        {
            if (_settingsPassword.Text != expected)
            {
                MessageBox.Show("Incorrect settings password.", "Access denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _passwordRevealed = true;
            _dbPassword.UseSystemPasswordChar = false;
            _revealPassword.Text = "Hide password";
            DisableSettings(false); // Enable all settings fields
            AddLog("Settings password unlocked - fields are now editable.");
        }
        else
        {
            _passwordRevealed = false;
            _dbPassword.UseSystemPasswordChar = true;
            _revealPassword.Text = "Show password";
            DisableSettings(true); // Disable all settings fields
            _settingsPassword.Clear();
            AddLog("Settings fields locked again.");
        }
    }

    private void StartTest()
    {
        if (_running) return;
        _running = true;
        SetStatus("TEST", Color.DarkOrange);

        // Read configuration
        string dut = _config.Get("Device", "DUT", "GPT9800-Arduino");
        string interface_ = _config.Get("Device", "Interface", "RS232");

        AddLog($"Starting test - DUT: {dut}, Interface: {interface_}");

        try
        {
            // Step 1: Connect to GPT9800 and Arduino (simulated)
            ConnectToDUT(dut, interface_);

            // Step 2: Wait for fixture to be ready (pogo pin connection)
            AddLog("Waiting for fixture connection via pogo pins...");
            System.Threading.Thread.Sleep(500); // Simulate wait time

            // Step 3: GPT9800 sends voltage for 0.5s
            AddLog("GPT9800 sending voltage pulse (0.5s)...");
            _cycleWatch.Restart();

            // Simulate the 0.5s voltage pulse
            var delayTask = System.Threading.Tasks.Task.Delay(500);
            while (!delayTask.IsCompleted)
            {
                Application.DoEvents();
            }

            // Step 4: Unit returns result
            string unitResult = SimulateUnitResponse();

            // Step 5: Compare with limits and show result
            CompareAndDisplayResult(unitResult);

            _running = false;
            SetStatus("IDLE", Color.FromArgb(110, 119, 129));
        }
        catch (Exception ex)
        {
            AddLog($"Test error: {ex.Message}");
            SetStatus("FAIL", Color.Firebrick);
            _running = false;
        }
    }

    private void ConnectToDUT(string dut, string interface_)
    {
        AddLog($"Connecting to {dut} on {interface_}...");

        // Simulate fixture control - in real implementation would control
        // Arduino via serial for pogo pin fixture management
        AddLog($"GPT9800 connection simulated on {dut} via {interface_}");

        // In real implementation:
        // - Connect to GPT9800 over LAN/USB for voltage generation
        // - Connect to Arduino on COM port for fixture/pogo pin control
    }

    private string SimulateUnitResponse()
    {
        // Simulate the unit returning a result after voltage is applied
        Random rnd = new Random(System.Environment.TickCount);
        bool pass = rnd.NextDouble() > 0.2; // 80% pass rate for demo
        string result = pass ? "PASS" : "FAIL";

        AddLog($"Unit returned result: {result}");
        return result;
    }

    private void CompareAndDisplayResult(string unitResult)
    {
        // Define limits for testing
        string[] lowLimits = { "1.00 kV", "0.0 mA", "100 MΩ", "0.00 Ω" };
        string[] highLimits = { "1.50 kV", "3.5 mA", "—", "0.20 Ω" };
        string[] values = { "1.50 kV", "2.1 mA", "150 MΩ", "0.12 Ω" };

        // Update grid with results
        for (int i = 0; i < _grid.Rows.Count; i++)
        {
            _grid.Rows[i].Cells["Value"].Value = values[i];
            _grid.Rows[i].Cells["LowLimit"].Value = lowLimits[i];
            _grid.Rows[i].Cells["UpLimit"].Value = highLimits[i];

            // Compare result
            bool rowPass = unitResult == "PASS";
            string finalResult = rowPass ? "PASS" : "FAIL";
            _grid.Rows[i].Cells["Result"].Value = finalResult;

            // Set color
            Color rowColor = rowPass ? Color.ForestGreen : Color.Firebrick;
            _grid.Rows[i].DefaultCellStyle.ForeColor = rowColor;
        }

        // Update counters
        _total++;
        if (unitResult == "PASS") { _pass++; }
        else { _fail++; }
        _fpyValue.Text = (_total == 0 ? "0.00%" : ($"{((double)_pass / _total * 100):F2}" + "%"));
        _totalValue.Text = _total.ToString();
        _failValue.Text = _fail.ToString();
        _passValue.Text = _pass.ToString();

        AddLog($"Test completed: {unitResult} | Pass: {_pass}, Fail: {_fail}");
    }

    private void StopTest()
    {
        if (_running)
        {
            _cycleWatch.Stop(); _running = false;
            foreach (DataGridViewRow row in _grid.Rows) row.Cells["Result"].Value = "STOP";
            SetStatus("IDLE", Color.FromArgb(110, 119, 129));
            AddLog("Test stopped by operator.");
        }
    }

    private void ResetCounters()
    {
        _pass = _fail = _total = 0;
        _passValue.Text = _failValue.Text = _totalValue.Text = "0"; _fpyValue.Text = "0.00%";
        AddLog("Counters reset.");
    }

    private void SetStatus(string text, Color color) { _status.Text = text; _status.BackColor = color; }
    private void DisableSettings(bool disabled)
    {
        _fixture.Enabled = !disabled;
        _server.Enabled = !disabled;
        _database.Enabled = !disabled;
        _dbUser.Enabled = !disabled;
        _dbPassword.Enabled = !disabled;
        _table.Enabled = !disabled;
        _dut.Enabled = !disabled;
        _interface.Enabled = !disabled;
        // Model and baudrate removed per requirements
    }
    private void AddLog(string message)
    {
        if (_log.IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength; _log.ScrollToCaret();
    }
}