namespace GPT9000_WinForms_TestSystem;

/// <summary>Nhãn tiếng Việt, tách khỏi mã trạng thái của thiết bị và báo cáo.</summary>
internal static class VietnameseText
{
    public const string ApplicationName = "Hệ thống kiểm tra an toàn điện GPT-9000";

    public static string GetApplicationName(string configuredName)
        => configuredName == "GPT-9000 Safety Test System" ? ApplicationName : configuredName;

    public static string PortCode(string displayedValue)
        => string.IsNullOrWhiteSpace(displayedValue) || displayedValue.Trim().Equals("Tự động", StringComparison.OrdinalIgnoreCase)
            ? "AUTO" : displayedValue.Trim();

    public static string Status(string code) => code switch
    {
        "IDLE" => "SẴN SÀNG",
        "NO DEV" => "CHƯA KẾT NỐI",
        "PREPARING" => "ĐANG CHUẨN BỊ",
        "NOT READY" => "CHƯA SẴN SÀNG",
        "TESTING" => "ĐANG KIỂM TRA",
        "STOP" => "ĐÃ DỪNG",
        "ERROR" => "LỖI",
        "MACHINE ERROR" => "LỖI THIẾT BỊ",
        _ => code
    };

    public static string Result(string code) => code.ToUpperInvariant() switch
    {
        "PASS" => "PASS",
        "FAIL" => "FAIL",
        "ERROR" => "Lỗi",
        _ => "Không xác định"
    };
}
