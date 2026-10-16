using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GPT9000_WinForms_TestSystem;

public partial class MainForm : Form
{
    private readonly IniConfig _config;
    private bool _passwordRevealed;
    private int _pass, _fail, _total;
    private readonly Stopwatch _cycleWatch = new();
    private bool _running;
    private DataGridView _grid = new();
    private RichTextBox _log = new();

    public MainForm()
    {
        InitializeComponent();
        // Initialize after designer components are created
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
        _config = new IniConfig(configPath);
        Text = _config.Get("Application", "AppName", "GPT-9000 Safety Test System");
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1450, 900);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(242, 245, 249);

        // Subscribe to tab switching for auto-save
        _tabs.Selecting += Tabs_Selecting;

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
        // Home tab is built by designer - no need to rebuild here
        // Just ensure status click works
        _status.Click += (_, _) => StartTest();
    }

    private void BuildSettingsTab()
    {
        // Settings tab is built by designer - no need to rebuild here
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
        AddLog($"GPT9800 connection simulated on {dut}");
    }

    private string SimulateUnitResponse()
    {
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