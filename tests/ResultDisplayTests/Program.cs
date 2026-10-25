using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using GPT9000_WinForms_TestSystem;

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
        _checks++;
    }

    [STAThread]
    private static void Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Application.EnableVisualStyles();
        // Không hiển thị form: không chạy Load, dò cổng serial hoặc phát áp.
        using var form = new MainForm();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Field(string name) => typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
        void SetField(string name, object value) => typeof(MainForm).GetField(name, flags)!.SetValue(form, value);
        object? Call(string name, params object?[] args) => typeof(MainForm).GetMethod(name, flags)!.Invoke(form, args);
        void FinishCycle()
        {
            var task = (Task)Call("ReturnToIdleAsync")!;
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(3))
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            if (!task.IsCompleted) throw new TimeoutException("Fixture completion did not finish");
            task.GetAwaiter().GetResult();
        }

        try
        {
            var grid = (DataGridView)Field("_grid");
            var program = (TestProgram)Field("_program");
            program.Name = "result-display-" + Guid.NewGuid().ToString("N");
            var report = (ReportWriter)Field("_report");
            HipotResult Measurement(int sequence) => new(0.5 + sequence * 0.01, sequence * 1.1, 2,
                sequence % 2 == 0 ? "FAIL" : "PASS", "test data");

            for (int sequence = 1; sequence <= 3; sequence++)
            {
                var measurement = Measurement(sequence);
                report.WriteReport(program.Name, sequence, program, measurement, DateTime.Now);
                SetField("_total", sequence);
                Call("AddResultRow", measurement);
            }
            Check(grid.Rows.Count == 3, "each measurement creates one data row");
            Check((string)grid.Rows[0].Cells[9].Value == "3.3" &&
                  (string)grid.Rows[1].Cells[9].Value == "2.2" &&
                  (string)grid.Rows[2].Cells[9].Value == "1.1", "latest measurement first, earlier results below");
            Check((string)grid.Rows[0].Cells[8].Value == "0.530" &&
                  (string)grid.Rows[0].Cells[11].Value == "PASS", "latest row contains measured voltage and verdict");
            Color highlight = grid.Rows[0].DefaultCellStyle.BackColor;
            Check(highlight != Color.White && grid.Rows[1].DefaultCellStyle.BackColor == Color.White &&
                  grid.Rows[2].DefaultCellStyle.BackColor == Color.White, "only latest row has a pale background");
            Check(grid.Rows[0].DefaultCellStyle.SelectionBackColor == highlight &&
                  grid.Rows[0].DefaultCellStyle.SelectionForeColor == grid.Rows[0].DefaultCellStyle.ForeColor,
                  "selection preserves latest-row background and PASS/FAIL text color");
            Check(grid.Columns.Cast<DataGridViewColumn>().All(c => c.SortMode == DataGridViewColumnSortMode.NotSortable),
                "column clicks cannot reorder the newest-first history");

            Call("LoadGridHistory");
            Check(grid.Rows.Count == 3 && (string)grid.Rows[0].Cells[9].Value == "3.3" &&
                  grid.Rows[0].DefaultCellStyle.BackColor == highlight &&
                  grid.Rows[1].DefaultCellStyle.BackColor == Color.White, "reloaded reports retain order and latest-row highlight");
            Check(((Label)Field("_lblTestItems")).Text == "LỊCH SỬ KẾT QUẢ", "measurement values appear in the Vietnamese grid, not the heading");
            for (int sequence = 4; sequence <= 205; sequence++) Call("AddResultRow", Measurement(sequence));
            Check(grid.Rows.Count == 200 && (string)grid.Rows[0].Cells[9].Value == "225.5" &&
                  grid.Rows.Cast<DataGridViewRow>().Count(r => r.DefaultCellStyle.BackColor == highlight) == 1,
                  "200-row limit retains the newest result and exactly one highlight");
            Check(grid.FirstDisplayedScrollingRowIndex == 0, "new measurements scroll the grid to the first row");

            var status = (Label)Field("_status");
            var hint = (Label)Field("_lblStartHint");
            foreach (var (text, displayed, color) in new[] { ("PASS", "PASS", Color.ForestGreen),
                         ("FAIL", "FAIL", Color.Firebrick), ("ERROR", "LỖI", Color.DarkRed) })
            {
                SetField("_running", true);
                Call("SetStatus", "TESTING", Color.Blue, "Measuring");
                Check(status.Text == "ĐANG KIỂM TRA", "next running test replaces previous verdict with Vietnamese testing status");
                Call("SetStatus", text, color, "Final outcome");
                SetField("_running", false);
                FinishCycle();
                Call("SetStatus", "IDLE", Color.Gray, "Ready");
                Call("OnHipotEvent", "InterLock");
                Check(status.Text == displayed && status.BackColor == color && hint.Text == "Final outcome",
                    text + " survives fixture completion and idle device notifications");
            }

            SetField("_running", true);
            Call("SetStatus", "TESTING", Color.Blue, "Measuring");
            Call("RequestStop", false);
            SetField("_running", false);
            FinishCycle();
            Check(status.Text == "ĐÃ DỪNG" && (bool)Field("_stopRequested"), "stopped cycle retains Vietnamese stop status");

            object deniedLicense = Activator.CreateInstance(Field("_license").GetType(),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { Path.Combine(AppContext.BaseDirectory, Guid.NewGuid() + ".dat"),
                    "TEST", Convert.ToBase64String(new byte[32]), "{}" }, CultureInfo.InvariantCulture)!;
            SetField("_license", deniedLicense);
            Call("StartTest", false);
            Check(status.Text == "ĐÃ DỪNG" && !(bool)Field("_running"), "denied START leaves the retained result unchanged");

            Check(form.Text == "Hệ thống kiểm tra an toàn điện GPT-9000" &&
                  ((TabPage)Field("_tabHome")).Text == "Trang chính" &&
                  grid.Columns[0].HeaderText == "Chương trình" && grid.Columns[11].HeaderText == "Kết quả",
                  "window, tab and result headers are Vietnamese");
            var password = (TextBox)Field("_settingsPassword");
            password.Text = "123456";
            Call("ToggleSettingsLock");
            ((ComboBox)Field("_hipotPort")).Text = "COM7";
            ((ComboBox)Field("_cbPort")).Text = "Tự động";
            Check((bool)Call("SaveSettings", true)!, "Vietnamese automatic port setting saves successfully");
            var saved = SystemSetting.Load(Path.Combine(AppContext.BaseDirectory, "Config", "SystemSetting.xml"));
            Check(saved.ComPortGPT == "COM7" && saved.ComPortControl == "AUTO",
                  "localized port choice persists the hardware-compatible AUTO code");
            Call("LoadSettingsToFields");
            Check(((ComboBox)Field("_cbPort")).Text == "Tự động", "reloaded AUTO setting displays in Vietnamese");

            var screenLog = (RichTextBox)Field("_log");
            var fileLog = (LogWriter)Field("_logFile");
            string screenBeforeHeartbeat = screenLog.Text;
            long bytesBeforeHeartbeat = new FileInfo(fileLog.CurrentPath).Length;
            Call("AddLog", "[CB] EVT HB");
            Check(screenLog.Text == screenBeforeHeartbeat && new FileInfo(fileLog.CurrentPath).Length == bytesBeforeHeartbeat,
                  "heartbeat reaches neither the screen nor the CSV log");
            _ = screenLog.Handle;
            string eventMessage = "[KIỂM TRA] đồng bộ thời gian " + Guid.NewGuid().ToString("N");
            Task.Run(() => Call("AddLog", eventMessage)).GetAwaiter().GetResult();
            Thread.Sleep(50); // Mô phỏng màn hình bận trong khi sự kiện đã được ghi vào tệp.
            Application.DoEvents();
            using var readLog = new FileStream(fileLog.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var readText = new StreamReader(readLog);
            string eventLine = readText.ReadToEnd().Split('\n').Last(line => line.Contains(eventMessage));
            string eventTime = eventLine.Split('|')[1].TrimStart().Split(' ')[0];
            Check(screenLog.Text.Contains($"[{eventTime}] {eventMessage}"),
                  "queued screen update uses the same captured event time as the file");
            Console.WriteLine($"{_checks} result display checks passed.");
        }
        finally
        {
            Call("Shutdown");
        }
    }
}
