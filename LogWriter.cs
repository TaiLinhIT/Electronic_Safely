using System.Text;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Ghi log vận hành ra file theo ngày, đặt trong <c>Log\</c> cạnh file .exe
/// (tức là trong thư mục bin khi chạy từ Visual Studio / <c>dotnet run</c>).
///
///   Log\2026-10-17_Log.csv
///
/// Mỗi dòng: <c>"Thoi_gian","Muc_do","Noi_dung"</c> - UTF-8 <b>CÓ BOM</b> (EF BB BF).
/// BOM là bắt buộc: Excel trên Windows mở file <c>.csv</c> không BOM bằng mã ANSI
/// (CP1258 trên máy Việt) và sẽ đọc tiếng Việt thành chuỗi rác như <c>BÆ°µc</c>
/// thay cho <c>Bước</c>. Có BOM thì Excel tự nhận diện UTF-8. Notepad, VS Code
/// và mọi trình đọc UTF-8 chuẩn vẫn mở bình thường.
///
/// File được xoay vòng theo ngày: sang ngày mới là tự mở file mới.
///
/// Ghi file thất bại (vd. đang bị Excel khóa) thì lớp này tự NGỪNG ghi và trả về
/// thông báo lỗi để UI cảnh báo một lần, chứ không làm sập phần mềm.
/// </summary>
public sealed class LogWriter : IDisposable
{
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string _date = "";
    private bool _suspended;

    /// <summary>Thư mục chứa file log.</summary>
    public string Dir { get; }

    public LogWriter(string dir) => Dir = dir;

    /// <summary>Đường dẫn file log của ngày hiện tại.</summary>
    public string CurrentPath => Path.Combine(Dir, $"{DateTime.Now:yyyy-MM-dd}_Log.csv");

    /// <summary>Lỗi gần nhất khi ghi file (rỗng nếu mọi thứ bình thường).</summary>
    public string LastError { get; private set; } = "";

    /// <summary>
    /// Ghi một dòng log. Trả về thông báo lỗi nếu không ghi được (null nếu OK).
    /// Gọi được từ bất kỳ thread nào.
    /// </summary>
    public string? Write(string message)
    {
        lock (_lock)
        {
            if (_suspended) return LastError.Length > 0 ? LastError : "đã ngừng ghi log";
            try
            {
                Directory.CreateDirectory(Dir);

                string date = DateTime.Now.ToString("yyyy-MM-dd");
                if (_writer is null || date != _date)
                {
                    _writer?.Dispose();
                    string path = CurrentPath;
                    EnsureBom(path);                       // chữa file thiếu BOM (bản cũ)
                    // UTF8Encoding(true) -> StreamWriter sẽ ghi preamble EF BB BF
                    // khi file trống, nhờ đó Excel nhận diện được UTF-8.
                    _writer = new StreamWriter(path, append: true, new UTF8Encoding(true));
                    _date = date;
                    if (_writer.BaseStream.Length == 0)
                        _writer.WriteLine("\"Thoi_gian\",\"Muc_do\",\"Noi_dung\"");
                }

                _writer.WriteLine(
                    $"\"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\",\"{LevelOf(message)}\",\"{Escape(message)}\"");
                _writer.Flush();
                LastError = "";
                return null;
            }
            catch (Exception ex)
            {
                // Gặp lỗi (thường là file đang bị Excel giữ) -> ngừng ghi để không lặp lỗi liên tục.
                LastError = $"Không ghi được file log ({ex.GetType().Name}): {ex.Message}";
                _suspended = true;
                return LastError;
            }
        }
    }

    /// <summary>Tự mở lại việc ghi (gọi sau khi người dùng đã đóng file Excel).</summary>
    public void Resume()
    {
        lock (_lock)
        {
            if (!_suspended) return;
            _suspended = false;
            LastError = "";
        }
    }

    /// <summary>Suy ra mức độ log từ nội dung, không cần sửa chỗ gọi.</summary>
    private static string LevelOf(string message)
    {
        if (message.Contains("[LỖI]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)) return "ERROR";
        if (message.Contains("[DỪNG]", StringComparison.OrdinalIgnoreCase)) return "STOP";
        if (message.Contains("[WARN]", StringComparison.OrdinalIgnoreCase)) return "WARN";
        if (message.Contains("[CT]", StringComparison.OrdinalIgnoreCase)) return "PROGRAM";
        if (message.Contains("[CB]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("[HIPOT]", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("[Bước", StringComparison.OrdinalIgnoreCase)) return "STEP";
        return "INFO";
    }

    /// <summary>
    /// File log ghi bởi bản cũ (UTF-8 không BOM) sẽ khiến Excel đọc tiếng Việt thành chuỗi rác
    /// như <c>BÆ°µc</c> thay cho <c>Bước</c>. Nếu phát hiện file đang mở thiếu BOM thì
    /// chép lại toàn bộ nội dung sang bản có BOM. Chạy một lần cho mỗi file theo ngày.
    /// </summary>
    private static void EnsureBom(string path)
    {
        if (!File.Exists(path)) return;
        if (new FileInfo(path).Length < 3) return;          // file rỗng -> StreamWriter tự ghi BOM

        byte[] head = new byte[3];
        using (var fs = File.OpenRead(path))
        {
            if (fs.Read(head, 0, 3) < 3) return;            // quá ngắn, cứ để StreamWriter lo
        }
        if (head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return;   // đã có BOM rồi

        byte[] body = File.ReadAllBytes(path);
        byte[] fixedBytes = new byte[3 + body.Length];
        fixedBytes[0] = 0xEF; fixedBytes[1] = 0xBB; fixedBytes[2] = 0xBF;
        Buffer.BlockCopy(body, 0, fixedBytes, 3, body.Length);
        File.WriteAllBytes(path, fixedBytes);
    }

    private static string Escape(string s) => s.Replace("\"", "\"\"");

    /// <summary>Gỡ lock file để người dùng mở được file log bằng Excel.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
