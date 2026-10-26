using System.Reflection;
using GPT9000_WinForms_TestSystem;

internal static class Program
{
    private static int _checks;
    private static void Check(bool ok, string text)
    {
        if (!ok) throw new Exception("FAIL: " + text);
        Console.WriteLine("PASS: " + text); _checks++;
    }
    private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Field(MainForm form, string name) => typeof(MainForm).GetField(name, Flags)!.GetValue(form)!;
    private static void Set(MainForm form, string name, object value) => typeof(MainForm).GetField(name, Flags)!.SetValue(form, value);
    private static object? Call(MainForm form, string name, params object[] args) => typeof(MainForm).GetMethod(name, Flags)!.Invoke(form, args);
    private static void Finish(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        if (!task.IsCompleted) throw new TimeoutException("Connection test did not complete.");
        task.GetAwaiter().GetResult();
    }
    private static FakeForm Form()
    {
        var form = new FakeForm();
        var paths = new AppPaths(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
        paths.Ensure(); Set(form, "_paths", paths);
        Set(form, "_setting", new SystemSetting()); Call(form, "LoadSettingsToFields");
        return form;
    }
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        var candidates = SerialPortDiscovery.Candidates(new[] { "COM4", "COM3", "COM9", "COM7", "com9" },
            new[] { "com9" }, new[] { "COM3", "COM4" });
        Check(candidates.SequenceEqual(new[] { "COM7" }), "automatic probing excludes the other device and Bluetooth ports");
        Check(SerialPortDiscovery.Candidates(new[] { "COM11", "COM2", "COM7" }, Array.Empty<string>(), Array.Empty<string>())
            .SequenceEqual(new[] { "COM2", "COM7", "COM11" }), "discovery visits wired ports deterministically");

        using (var form = Form())
        {
            var task = (Task)Call(form, "RunStartupChecksAsync")!;
            try
            {
                Check((bool)Field(form, "_connecting") && !task.IsCompleted, "startup holds the connection gate across awaits");
                Check(!((Button)Field(form, "_btnReconnect")).Enabled && !((Button)Field(form, "_btnUnlockSettings")).Enabled,
                    "reconnect and settings controls are disabled during discovery");
                Finish((Task)Call(form, "RunStartupChecksAsync")!);
                Finish((Task)Call(form, "ReconnectAsync")!);
                Check(form.BoardCalls == 0, "reentrant startup/reconnect cannot begin another device connection");
                Check(!(bool)Call(form, "SaveSettings", true)!, "settings cannot be saved while serial discovery is running");
                Call(form, "StartTest", false); Call(form, "StartTest", true);
                Check(!(bool)Field(form, "_running") && form.Notices == 0 && ((DataGridView)Field(form, "_grid")).Rows.Count == 0,
                    "screen and board START cannot run a test during connection");
            }
            finally { form.AllowMeter.Set(); }
            Finish(task);
            Check(form.MeterCalls == 1 && form.BoardCalls == 1, "only one meter and board attempt ran despite repeated requests");
            Check(!(bool)Field(form, "_connecting") && ((Button)Field(form, "_btnReconnect")).Enabled,
                "controls recover after a successful connection");
            Check(!((Button)Field(form, "_btnSaveSettings")).Enabled, "locked settings remain locked after connection");
            form.ThrowMeter = true;
            Finish((Task)Call(form, "ReconnectAsync")!);
            Check(!(bool)Field(form, "_connecting") && ((Button)Field(form, "_btnReconnect")).Enabled,
                "an unexpected connection exception releases the gate and restores controls");
            form.ThrowMeter = false;
            form.MeterOk = false;
            Finish((Task)Call(form, "ReconnectAsync")!);
            Check(!(bool)Field(form, "_hipotOk") && (bool)Field(form, "_cbOk"),
                "a missing meter still permits diagnosing the board without reporting the meter connected");
            int before = form.MeterCalls;
            Set(form, "_running", true);
            Finish((Task)Call(form, "ReconnectAsync")!);
            Check(form.MeterCalls == before, "reconnect cannot interrupt a running measurement");
            Set(form, "_running", false);
            Call(form, "Shutdown");
        }
        using (var form = Form())
        {
            var task = (Task)Call(form, "RunStartupChecksAsync")!;
            Call(form, "Shutdown"); form.AllowMeter.Set(); Finish(task);
            Check(form.BoardCalls == 0 && !(bool)Field(form, "_connecting"),
                "closing during discovery does not connect more hardware or leave the gate held");
        }
        Console.WriteLine($"{_checks} connection checks passed.");
    }
    private sealed class FakeForm : MainForm
    {
        public readonly ManualResetEventSlim AllowMeter = new();
        public int MeterCalls, BoardCalls, Notices;
        public bool MeterOk = true, ThrowMeter;
        protected override bool ConnectHipot()
        {
            Interlocked.Increment(ref MeterCalls);
            if (!AllowMeter.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("Fake meter stalled.");
            if (ThrowMeter) throw new IOException("Injected driver failure.");
            return MeterOk;
        }
        protected override bool ConnectControlBoard() { Interlocked.Increment(ref BoardCalls); return true; }
        protected override void ShowStartError(string text) { Notices++; }
    }
}
