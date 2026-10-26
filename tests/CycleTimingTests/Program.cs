using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using GPT9000_WinForms_TestSystem;

internal static class Program
{
    private static int _checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        _checks++;
    }

    private static string Reply(CycleTimingSettings t) => FormattableString.Invariant(
        $"TIMING V=1 CLOSE={t.FixtureCloseMs} OUT={t.PogoOutMs} BACK={t.PogoBackMs} OPEN={t.FixtureOpenMs} ARM={t.ArmWaitMs}");
    private static ArduinoLink.Reply Ok(string text) => new(true, text, "OK " + text);

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        string root = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var defaults = new CycleTimingSettings();
        Check(defaults.TryValidate(out _) && defaults.UsesDefaultBoardTiming, "defaults preserve the previous cycle timings");
        string path = Path.Combine(root, "SystemSetting.xml");
        File.WriteAllText(path, "<SystemSetting><ComPortGPT>COM12</ComPortGPT><ComPortControl>COM15</ComPortControl></SystemSetting>");
        var old = SystemSetting.Load(path);
        Check(old.Timing.UsesDefaultBoardTiming && old.ComPortGPT == "COM12" && old.ComPortControl == "COM15",
            "older XML keeps COM ports and supplies timing defaults");
        old.Timing.FixtureCloseMs = 2755;
        old.Timing.MeasurementReadyDelayMs = 1765;
        old.Save(path);
        var restored = SystemSetting.Load(path);
        Check(restored.Timing.FixtureCloseMs == 2755 && restored.Timing.MeasurementReadyDelayMs == 1765,
            "XML preserves millisecond precision across restart");
        string previous = File.ReadAllText(path);
        restored.Timing.PogoBackMs = 0;
        bool rejected = false;
        try { restored.Save(path); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && File.ReadAllText(path) == previous, "invalid configuration cannot overwrite a saved file");
        File.WriteAllText(path, "<SystemSetting><ComPortGPT>COM19</ComPortGPT><Timing><FixtureCloseMs>-1</FixtureCloseMs></Timing></SystemSetting>");
        restored = SystemSetting.Load(path);
        Check(restored.ComPortGPT == "COM19" && restored.Timing.FixtureCloseMs == 2500,
            "invalid timing XML falls back without losing the port configuration");
        foreach (var invalid in new[]
        {
            new CycleTimingSettings { PogoOutMs = 99 }, new CycleTimingSettings { FixtureOpenMs = 7501 },
            new CycleTimingSettings { ArmWaitMs = 999 }, new CycleTimingSettings { ArmWaitMs = 30001 },
            new CycleTimingSettings { MeasurementReadTimeoutMs = 499 }, new CycleTimingSettings { PostCycleDelayMs = -1 },
            new CycleTimingSettings { MeasurementReadyDelayMs = 5000 },
            new CycleTimingSettings { PogoBackMs = 5000, FixtureOpenMs = 4000 }
        }) Check(!invalid.TryValidate(out string error) && error.Length > 0, "invalid timing or incompatible wait rejected");
        var custom = new CycleTimingSettings
        {
            FixtureCloseMs = 3500, PogoOutMs = 600, PogoBackMs = 900, FixtureOpenMs = 1800, ArmWaitMs = 2000,
            TestCompletionMarginMs = 7000
        };
        Check(custom.CompletionTimeoutMs(new TestProgram { RampSec = 0.5, TimeSec = 2 }) == 9500,
            "completion budget includes the configured margin");
        var commands = new List<string>();
        var applied = FixtureTimingProtocol.Apply(custom, command =>
        {
            commands.Add(command);
            return Ok(Reply(command == "TIMING?" ? defaults : custom));
        });
        Check(applied.Ready && applied.Applied && commands.SequenceEqual(new[] { "TIMING?", "TIMING SET 3500 600 900 1800 2000" }),
            "PC sends all board values atomically and checks the returned values");
        commands.Clear();
        applied = FixtureTimingProtocol.Apply(custom, command => { commands.Add(command); return Ok(Reply(custom)); });
        Check(applied.Applied && commands.Count == 1, "matching timings do not require another board write");
        var legacy = new ArduinoLink.Reply(false, "UNKNOWN_CMD TIMING?", "ERR UNKNOWN_CMD TIMING?");
        applied = FixtureTimingProtocol.Apply(defaults, _ => legacy);
        Check(applied.Ready && !applied.Applied, "old firmware can retain default timing without claiming to apply it");
        Check(!FixtureTimingProtocol.Apply(custom, _ => legacy).Ready, "custom board timing requires compatible firmware");
        Check(!FixtureTimingProtocol.Apply(defaults, _ => new(false, "", "[timeout] TIMING?")).Ready,
            "a timeout cannot be mistaken for an old board using defaults");
        applied = FixtureTimingProtocol.Apply(custom, command => command == "TIMING?"
            ? Ok(Reply(defaults)) : new(false, "TIMING_BUSY", "ERR TIMING_BUSY"));
        Check(!applied.Ready && applied.Message.Contains("hoạt động"), "busy board does not accept changed timing");
        Check(!FixtureTimingProtocol.Apply(custom, _ => Ok("TIMING V=2 CLOSE=3500")).Ready,
            "unknown or incomplete timing response is rejected");
        applied = FixtureTimingProtocol.Apply(custom, _ => Ok(Reply(defaults)));
        Check(!applied.Ready, "an OK reply with different values cannot confirm the requested timing");
        int sent = 0;
        Check(!FixtureTimingProtocol.Apply(new() { ArmWaitMs = 0 }, _ => { sent++; return Ok(Reply(defaults)); }).Ready && sent == 0,
            "invalid PC settings never reach the board");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        Check(FixtureTimingProtocol.SetCommand(custom) == "TIMING SET 3500 600 900 1800 2000", "wire format is independent of display culture");

        if (args.Length > 0) TestNativeFirmware(args[0], custom);
        TestSettingsUi(root);
        Console.WriteLine($"{_checks} timing checks passed.");
    }

    private static void TestNativeFirmware(string exe, CycleTimingSettings timing)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.GetFullPath(exe), "--protocol")
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        })!;
        string ReadLine() => process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()
            ?? throw new IOException("Firmware simulator closed unexpectedly.");
        try
        {
            Check(ReadLine() == "SIM READY", "native firmware simulator starts without physical hardware");
            ArduinoLink.Reply Send(string command)
            {
                process.StandardInput.WriteLine(command); process.StandardInput.Flush();
                string line = ReadLine();
                Console.WriteLine("SIM: " + command + " -> " + line);
                return new(line.StartsWith("OK "), line.Length > 3 ? line[3..] : "", line);
            }
            var result = FixtureTimingProtocol.Apply(timing, Send);
            Check(result.Ready && result.Applied, "real C# protocol applies settings to the actual firmware source");
            Check(FixtureTimingProtocol.Matches(Send("TIMING?").Text, timing), "native firmware readback matches all requested values");
            Check(Send("STATUS?").Text.Contains("RELAY=OFF") && Send("FIX STATUS").Text.Contains("STATE=IDLE"),
                "PC-to-firmware configuration leaves relay off and fixture idle");
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(5000)) process.Kill();
        }
    }

    private static void TestSettingsUi(string root)
    {
        Application.EnableVisualStyles();
        // Do not show the form or invoke Load: no serial connection or physical test.
        using var form = new MainForm();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object Field(string name) => typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
        void Set(string name, object value) => typeof(MainForm).GetField(name, flags)!.SetValue(form, value);
        object? Call(string name, params object[] values) => typeof(MainForm).GetMethod(name, flags)!.Invoke(form, values);
        var paths = new AppPaths(Path.Combine(root, "ui")); paths.Ensure();
        var initial = new SystemSetting
        {
            Timing = new()
            {
                ArmWaitMs = 22000, MeasurementReadyDelayMs = 1750, TestCompletionMarginMs = 6500,
                MeasurementReadTimeoutMs = 4500, ErrorHoldMs = 850, PostCycleDelayMs = 75
            }
        };
        Set("_paths", paths); Set("_setting", initial); Call("LoadSettingsToFields");
        var inputs = (Dictionary<string, NumericUpDown>)Field("_timingInputs");
        var password = (TextBox)Field("_settingsPassword");
        void Unlock() { password.Text = "123456"; Call("ToggleSettingsLock"); }
        bool Save() => (bool)Call("SaveSettings", true)!;
        try
        {
            Check(inputs.Count == 4 && inputs.Values.All(n => !n.Enabled), "only the four mechanical timing fields are shown and start locked");
            Check(inputs[nameof(CycleTimingSettings.FixtureCloseMs)].Value == 2.5M
                && inputs[nameof(CycleTimingSettings.PogoOutMs)].Value == 1.2M, "UI displays existing board defaults in seconds");
            Check(Save() && !File.Exists(paths.SettingFile), "locked settings cannot write timing values");
            Unlock();
            Check(inputs.Values.All(n => n.Enabled), "normal settings password unlocks timing fields");
            inputs[nameof(CycleTimingSettings.FixtureCloseMs)].Value = 2.755M;
            inputs[nameof(CycleTimingSettings.PogoOutMs)].Value = 1.350M;
            Check(Save(), "offline timing changes save without opening serial ports");
            var saved = SystemSetting.Load(paths.SettingFile);
            Check(saved.Timing.FixtureCloseMs == 2755 && saved.Timing.PogoOutMs == 1350,
                "UI seconds convert to exact millisecond values in the saved file");
            Check(saved.Timing.ArmWaitMs == 22000 && saved.Timing.MeasurementReadyDelayMs == 1750
                && saved.Timing.TestCompletionMarginMs == 6500 && saved.Timing.MeasurementReadTimeoutMs == 4500
                && saved.Timing.ErrorHoldMs == 850 && saved.Timing.PostCycleDelayMs == 75
                && saved.Timing.FixtureReturnTimeoutMs == 8000,
                "saving the compact UI preserves the hidden technical timings from the existing configuration");
            Check(inputs.Values.All(n => !n.Enabled) && !((SettingsAccess)Field("_settingsAccess")).Unlocked,
                "saving locks the timing fields again");
            Check(((Label)Field("_timingStatus")).Text.Contains("kết nối"), "offline save clearly indicates board synchronization is pending");
            Unlock();
            string xml = File.ReadAllText(paths.SettingFile);
            foreach (string busy in new[] { "_running", "_fixtureStartPending", "_returningToIdle" })
            {
                Set(busy, true);
                Check(!Save() && File.ReadAllText(paths.SettingFile) == xml, "saving blocked during " + busy);
                Set(busy, false);
            }
            inputs[nameof(CycleTimingSettings.PogoBackMs)].Value = 7.5M;
            inputs[nameof(CycleTimingSettings.FixtureOpenMs)].Value = 7.5M;
            Check(Save(), "longer mechanical times save without requiring an advanced wait field");
            saved = SystemSetting.Load(paths.SettingFile);
            Check(saved.Timing.FixtureReturnTimeoutMs == 15500 && saved.Timing.TryValidate(out _),
                "return timeout automatically allows both return strokes plus the safety margin");
            Unlock();
            inputs[nameof(CycleTimingSettings.PogoBackMs)].Value = 1.2M;
            inputs[nameof(CycleTimingSettings.FixtureOpenMs)].Value = 2.5M;
            Check(Save(), "shorter mechanical times keep an existing larger return wait budget");
            Call("LoadSettingsToFields");
            Check(inputs[nameof(CycleTimingSettings.FixtureCloseMs)].Value == 2.755M, "reload restores the previously saved timing");

            var tabs = (TabControl)Field("_tabs");
            var settings = (TabPage)Field("_tabSettings");
            tabs.SelectedTab = settings;
            _ = form.Handle;
            Unlock();
            var layout = settings.Controls.OfType<TableLayoutPanel>().Single();
            // Detach the layout to render it without showing MainForm or running Load.
            settings.Controls.Remove(layout);
            layout.Dock = DockStyle.None;
            layout.Visible = true;
            layout.CreateControl();
            foreach (int width in new[] { 1450, 1100 })
            {
                layout.Width = width - 28;
                layout.Height = layout.GetPreferredSize(new Size(layout.Width, 0)).Height;
                layout.PerformLayout();
                Check(layout.Controls.Cast<Control>().Where(c => c.Visible).All(c => c.Bottom <= layout.Height),
                    "all settings sections fit the scrollable layout at width " + width);
                Check(inputs.Values.All(n => n.Width > 70 && n.Height >= n.PreferredHeight),
                    "numeric fields fit at window width " + width);
                using var bitmap = new Bitmap(layout.Width, layout.Height);
                layout.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                string preview = Path.Combine(root, $"settings-{width}.png");
                bitmap.Save(preview, ImageFormat.Png);
                Console.WriteLine("PREVIEW: " + preview);
            }
            layout.Dock = DockStyle.Top;
            settings.Controls.Add(layout);
            Call("LockSettings");
            Set("_fixtureTimingReady", true);
            Call("OnBoardEvent", "OK BOOT GPT9800-CB FW=1.4.0 FIXTURE=ON");
            Check(!(bool)Field("_fixtureTimingReady") && ((Label)Field("_timingStatus")).Text.Contains("khởi động lại"),
                "a board reboot invalidates the timing confirmation until configuration is restored");
            Call("OnBoardEvent", "EVT CYCLE_CLOSING");
            Check((bool)Field("_fixtureStartPending"), "physical START marks the closing phase busy before TESTON");
            Call("OnBoardEvent", "EVT CYCLE_IDLE");
            Check(!(bool)Field("_fixtureStartPending"), "physical fixture returning idle clears the pending phase");
            var before = (CycleTimingSettings)Field("_setting").GetType().GetProperty("Timing")!.GetValue(Field("_setting"))!;
            var elapsed = Stopwatch.StartNew();
            var wait = (Task)Call("ReturnToIdleAsync")!;
            Check((bool)Field("_returningToIdle"), "post-cycle waiting prevents a new cycle from starting early");
            while (!wait.IsCompleted && elapsed.ElapsedMilliseconds < 3000) { Application.DoEvents(); Thread.Sleep(5); }
            wait.GetAwaiter().GetResult();
            Check(elapsed.ElapsedMilliseconds >= before.PostCycleDelayMs && !(bool)Field("_returningToIdle"),
                "post-cycle wait uses the configured value and releases the busy flag");
            Set("_fixtureStartPending", true);
            Call("RequestStop", false);
            elapsed.Restart();
            while ((bool)Field("_returningToIdle") && elapsed.ElapsedMilliseconds < 3000)
            { Application.DoEvents(); Thread.Sleep(5); }
            Check((bool)Field("_stopRequested") && !(bool)Field("_fixtureStartPending") && !(bool)Field("_running"),
                "STOP also cancels the preparation phase while new fixture timings are in effect");
        }
        finally { Call("Shutdown"); }
    }
}
