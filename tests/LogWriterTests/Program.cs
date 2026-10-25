using System.Globalization;
using System.Text;
using GPT9000_WinForms_TestSystem;
using Microsoft.VisualBasic.FileIO;

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        _checks++;
    }

    private static List<string[]> ReadCsv(string path)
    {
        using var parser = new TextFieldParser(path, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(",");
        var rows = new List<string[]>();
        while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
        return rows;
    }

    private static void Main()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        foreach (string noise in new[] { "", "  ", "[CB] EVT HB", " EVT HB ", "[cb] evt hb UPTIME=123", "[CB] EVT\tHB" })
            Check(!LogWriter.ShouldLog(noise), "routine heartbeat/blank message filtered: " + noise);
        foreach (string important in new[] { "[CB] EVT HB_TIMEOUT", "[CB] EVT HB ERROR=INTERLOCK_OPEN",
                     "[CB] EVT HB WARN=1", "[CB] EVT STROKE_TIMEOUT", "[CB] EVT UNIT_LOST",
                     "[CB] EVT HV_RELEASED", "[CB] EVT RESULT_FAIL", "[KẾT QUẢ] PASS", "[DỪNG] Người vận hành dừng" })
            Check(LogWriter.ShouldLog(important), "operational/fault message retained: " + important);

        var beforeMidnight = new DateTimeOffset(2026, 10, 24, 23, 59, 59, 987, TimeSpan.FromHours(7));
        var afterMidnight = new DateTimeOffset(2026, 10, 25, 0, 0, 0, 123, TimeSpan.FromHours(7));
        string events = Path.Combine(root, "events");
        string quoted = "[LỖI] Cổng COM7, phản hồi \"không hợp lệ\"\nĐã dừng.";
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            using var writer = new LogWriter(events);
            Check(Path.GetFileName(writer.CurrentPath).StartsWith(DateTime.Now.Year.ToString(CultureInfo.InvariantCulture)),
                "current log path uses the Gregorian year regardless of system culture");
            Check(writer.Write("[CB] EVT HB", beforeMidnight) is null && !Directory.Exists(events),
                "heartbeat creates no log file or directory");
            Check(writer.Write("[KẾT QUẢ] PASS", beforeMidnight) is null, "result written successfully");
            Check(writer.Write(quoted, afterMidnight) is null, "multiline error written after midnight");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }

        string firstFile = Path.Combine(events, "2026-10-24_Log.csv");
        string secondFile = Path.Combine(events, "2026-10-25_Log.csv");
        Check(File.Exists(firstFile) && File.Exists(secondFile), "rotation uses event local date rather than current/UTC date");
        var firstRows = ReadCsv(firstFile);
        var secondRows = ReadCsv(secondFile);
        Check(firstRows.Count == 2 && secondRows.Count == 2 && firstRows.All(r => r.Length == 3),
            "one CSV header and event per day, without heartbeat rows");
        Check(firstRows[1][0] == "24/10/2026 | 23:59:59.987 (UTC+07:00)" &&
              secondRows[1][0] == "25/10/2026 | 00:00:00.123 (UTC+07:00)",
              "timestamps retain day, hour, milliseconds and offset in a text field for Excel");
        Check(secondRows[1][2] == quoted && secondRows[1][1] == "ERROR", "CSV preserves Vietnamese, commas, quotes and line breaks");
        Check(File.ReadAllBytes(firstFile).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
            "CSV retains UTF-8 BOM for Vietnamese in Excel");

        string legacyDir = Path.Combine(root, "legacy");
        Directory.CreateDirectory(legacyDir);
        string legacyFile = Path.Combine(legacyDir, "2026-10-24_Log.csv");
        string legacy = "\"Thoi_gian\",\"Muc_do\",\"Noi_dung\"\n\"2026-10-24 17:00:00.000\",\"INFO\",\"Dữ liệu cũ\"\n";
        File.WriteAllText(legacyFile, legacy, new UTF8Encoding(false));
        using (var writer = new LogWriter(legacyDir)) writer.Write("[CT] Dữ liệu mới", beforeMidnight);
        var legacyRows = ReadCsv(legacyFile);
        Check(legacyRows.Count == 3 && legacyRows[1][0] == "2026-10-24 17:00:00.000" && legacyRows[1][2] == "Dữ liệu cũ",
            "appending preserves old log records and adds no duplicate header");

        string lockedDir = Path.Combine(root, "locked");
        Directory.CreateDirectory(lockedDir);
        string lockedFile = Path.Combine(lockedDir, "2026-10-24_Log.csv");
        using (var writer = new LogWriter(lockedDir))
        {
            using (var held = new FileStream(lockedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                Check(writer.Write("[LỖI] Kiểm tra tệp bị khóa", beforeMidnight) is not null, "locked log reports an error without throwing");
            writer.Resume();
            Check(writer.Write("[KẾT QUẢ] FAIL", beforeMidnight) is null && writer.LastError.Length == 0,
                "logging resumes after the file is unlocked");
        }

        string parallelDir = Path.Combine(root, "parallel");
        using (var writer = new LogWriter(parallelDir))
            Parallel.For(0, 100, i =>
            {
                if (writer.Write($"[KẾT QUẢ] PASS {i}", beforeMidnight) is not null) throw new Exception("Concurrent write failed");
                writer.Write("[CB] EVT HB", beforeMidnight);
            });
        var parallelRows = ReadCsv(Path.Combine(parallelDir, "2026-10-24_Log.csv"));
        Check(parallelRows.Count == 101 && parallelRows.Skip(1).Select(r => r[2]).Distinct().Count() == 100,
            "concurrent logging retains every result once and filters every heartbeat");
        Console.WriteLine($"{_checks} log writer checks passed.");
    }
}
