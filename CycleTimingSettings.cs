using System.Globalization;

namespace GPT9000_WinForms_TestSystem;

/// <summary>Milliseconds persisted with the serial configuration; UI displays seconds.</summary>
public sealed class CycleTimingSettings
{
    public int FixtureCloseMs { get; set; } = 2500;
    public int PogoOutMs { get; set; } = 1200;
    public int PogoBackMs { get; set; } = 1200;
    public int FixtureOpenMs { get; set; } = 2500;
    public int ArmWaitMs { get; set; } = 30000;
    public int MeasurementReadyDelayMs { get; set; } = 1500;
    public int TestCompletionMarginMs { get; set; } = 5000;
    public int MeasurementReadTimeoutMs { get; set; } = 4000;
    public int ErrorHoldMs { get; set; } = 1200;
    public int PostCycleDelayMs { get; set; } = 300;
    public int FixtureReturnTimeoutMs { get; set; } = 8000;

    public bool TryValidate(out string error)
    {
        error = "";
        if (new[] { FixtureCloseMs, PogoOutMs, PogoBackMs, FixtureOpenMs }.Any(v => v is < 100 or > 7500))
            error = "Thời gian đóng/mở đồ gá và đẩy/rút kim phải từ 0,1 đến 7,5 giây.";
        else if (ArmWaitMs is < 1000 or > 30000)
            error = "Thời gian chờ xác nhận máy thử phải từ 1 đến 30 giây.";
        else if (MeasurementReadyDelayMs is < 0 or > 10000 || TestCompletionMarginMs is < 1000 or > 60000
            || MeasurementReadTimeoutMs is < 500 or > 30000 || ErrorHoldMs is < 0 or > 10000
            || PostCycleDelayMs is < 0 or > 10000 || FixtureReturnTimeoutMs is < 1000 or > 30000)
            error = "Thời gian chờ nằm ngoài giới hạn cho phép.";
        else if (TestCompletionMarginMs < MeasurementReadyDelayMs + 500)
            error = "Dự phòng chờ hoàn tất phải lớn hơn thời gian chờ trước đọc kết quả ít nhất 0,5 giây.";
        else if (FixtureReturnTimeoutMs < PogoBackMs + FixtureOpenMs + 500)
            error = "Chờ đồ gá về phải lớn hơn tổng thời gian rút kim và mở đồ gá ít nhất 0,5 giây.";
        return error.Length == 0;
    }

    public int CompletionTimeoutMs(TestProgram program) =>
        (int)Math.Clamp((program.RampSec + program.TimeSec) * 1000 + TestCompletionMarginMs, 3000, 120000);

    internal bool UsesDefaultBoardTiming => FixtureCloseMs == 2500 && PogoOutMs == 1200
        && PogoBackMs == 1200 && FixtureOpenMs == 2500 && ArmWaitMs == 30000;
}

internal sealed record FixtureTimingResult(bool Ready, bool Applied, string Message);

internal static class FixtureTimingProtocol
{
    public static string SetCommand(CycleTimingSettings timing) => FormattableString.Invariant(
        $"TIMING SET {timing.FixtureCloseMs} {timing.PogoOutMs} {timing.PogoBackMs} {timing.FixtureOpenMs} {timing.ArmWaitMs}");

    internal static bool Matches(string text, CycleTimingSettings timing)
    {
        if (!text.StartsWith("TIMING ", StringComparison.Ordinal)) return false;
        bool Field(string name, int expected) => int.TryParse(ArduinoLink.GetField(text, name),
            NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value == expected;
        return Field("V", 1) && Field("CLOSE", timing.FixtureCloseMs) && Field("OUT", timing.PogoOutMs)
            && Field("BACK", timing.PogoBackMs) && Field("OPEN", timing.FixtureOpenMs) && Field("ARM", timing.ArmWaitMs);
    }

    internal static FixtureTimingResult Apply(CycleTimingSettings timing, Func<string, ArduinoLink.Reply> send)
    {
        if (!timing.TryValidate(out string error)) return new(false, false, error);
        var current = send("TIMING?");
        if (!current.Ok)
        {
            if (current.Raw.StartsWith("ERR UNKNOWN_CMD", StringComparison.Ordinal))
                return new(timing.UsesDefaultBoardTiming, false,
                    "Bảng điều khiển chưa hỗ trợ chỉnh thời gian. Cần nạp phần mềm điều khiển 1.4.0; bản cũ chỉ dùng thời gian mặc định.");
            return new(false, false, "Chưa đọc được thời gian từ bảng điều khiển. Kiểm tra kết nối rồi áp dụng lại.");
        }
        if (!current.Text.StartsWith("TIMING ", StringComparison.Ordinal)
            || ArduinoLink.GetField(current.Text, "V") != "1"
            || new[] { "CLOSE", "OUT", "BACK", "OPEN", "ARM" }.Any(name =>
                !int.TryParse(ArduinoLink.GetField(current.Text, name), NumberStyles.None,
                    CultureInfo.InvariantCulture, out _)))
            return new(false, false, "Phản hồi thời gian không hợp lệ. Kiểm tra phần mềm điều khiển trên bảng điều khiển.");
        if (Matches(current.Text, timing)) return new(true, true, "Bảng điều khiển đã xác nhận các thời gian đang cài đặt.");
        var reply = send(SetCommand(timing));
        if (reply.Ok && Matches(reply.Text, timing))
            return new(true, true, "Đã áp dụng thời gian xuống bảng điều khiển.");
        return new(false, false, reply.Raw.Contains("TIMING_BUSY", StringComparison.Ordinal)
            ? "Đồ gá đang hoạt động. Chờ đồ gá mở hoàn toàn rồi lưu lại."
            : "Bảng điều khiển chưa xác nhận thời gian mới. Kiểm tra phần mềm điều khiển và kết nối rồi áp dụng lại.");
    }
}
