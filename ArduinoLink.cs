using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace GPT9000_WinForms_TestSystem;

/// <summary>
/// Kết nối thật tới Arduino Control Board (firmware GPT9800_ControlBoard) qua cổng serial.
///
/// Giao thức ASCII theo dòng (xem arduino\GPT9800_ControlBoard\GPT9800_ControlBoard.ino):
///   Host -> Board : "PING"
///   Board -> Host : "OK PONG GPT9800-CB FW=1.0.0"   (phản hồi có tiền tố "OK " / "ERR ")
///   Board -> Host : "EVT INTERLOCK_OPEN"            (sự kiện không chủ động)
///
/// Mọi câu trả lời kết thúc bằng '\n'. Board cũng gửi "OK BOOT ..." mỗi lần mở cổng
/// (Arduino tự reset khi DTR được bật) - parser của lớp này đã loại bỏ các dòng thừa.
/// </summary>
public sealed class ArduinoLink : IDisposable
{
    /// <summary>Phản hồi của Board. Trả về phần sau "OK " nếu thành công; null nếu "ERR ..." hoặc timeout.</summary>
    public sealed record Reply(bool Ok, string Text, string Raw);

    /// <summary>Gửi cảnh báo tới UI khi Board đẩy sự kiện (EVT ...) hoặc dòng thô.</summary>
    public event Action<string>? EventReceived;

    /// <summary>Dòng thô nhận được (dùng để ghi log chi tiết).</summary>
    public event Action<string>? RawLineReceived;

    private SerialPort? _port;
    private Thread? _reader;
    private volatile bool _stop;
    private readonly BlockingCollection<string> _replies = new(new ConcurrentQueue<string>());
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromMilliseconds(1500);

    public bool IsOpen => _port?.IsOpen == true;
    public string? PortName => _port?.PortName;
    public string Firmware { get; private set; } = "";
    public string BoardId { get; private set; } = "";

    /// <summary>Danh sách cổng serial đang có trên máy.</summary>
    public static string[] GetPorts() => SerialPort.GetPortNames();

    /// <summary>
    /// Kết nối tới một cổng cụ thể. Trả về true nếu Board trả lời "OK PONG".
    /// </summary>
    public bool Connect(string portName, int baudRate, out string error, int timeoutMs = 2000)
    {
        Disconnect();
        error = "";
        try
        {
            var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                NewLine = "\n",
                ReadTimeout = 250,
                WriteTimeout = 500,
                DtrEnable = true,   // cần true: board AVR reset khi mở cổng
                RtsEnable = true
            };
            port.Open();
            _port = port;
            _stop = false;
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = $"arduino-{portName}" };
            _reader.Start();

            // Board tự reset khi mở cổng (DTR) -> đợi bootloader xong rồi mới gửi lệnh.
            // Thử PING lặp lại thay vì ngủ cứng 2000 ms:
            //   - board thật: optiboot xong sau ~1-1.5 s -> thoát ngay khi có "OK PONG"
            //   - cổng chết / COM Bluetooth: hết ngân sách là fail
            // Nhờ vậy AutoConnect không mất thêm ~2 s ngủ + timeout cho MỖI cổng trống.
            int budgetMs = Math.Max(timeoutMs + 600, 1800);
            Reply r = new(false, "", "");
            bool ponged = false;
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < budgetMs)
            {
                r = SendCommand("PING", 350);
                // Phải đúng câu trả lời của PING ("OK PONG ..."). Dòng "OK BOOT ..."
                // do board tự gửi khi reset KHÔNG được tính, nếu không Firmware sẽ
                // hiển thị sai và cổng Bluetooth trả lời linh tinh có thể lọt vào.
                if (r.Ok && r.Text.Contains("PONG", StringComparison.OrdinalIgnoreCase))
                {
                    ponged = true;
                    break;
                }
                Thread.Sleep(100);
            }

            if (!ponged)
            {
                error = r.Raw.Length == 0 || r.Raw.StartsWith("[timeout", StringComparison.Ordinal)
                    ? $"Không có phản hồi PING từ {portName}"
                    : r.Raw;
                Disconnect();
                return false;
            }

            Firmware = r.Text;
            Reply id = SendCommand("ID?", timeoutMs);
            if (id.Ok) BoardId = id.Text;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Disconnect();
            return false;
        }
    }

    /// <summary>Quét các cổng serial, trả về cổng đầu tiên trả lời đúng "OK PONG".</summary>
    public bool AutoConnect(int baudRate, out string portName, out string error, int timeoutMs = 1200)
    {
        portName = "";
        error = "Không tìm thấy cổng serial nào.";
        foreach (string p in GetPorts())
        {
            try
            {
                if (Connect(p, baudRate, out string e, timeoutMs))
                {
                    portName = p;
                    error = "";
                    return true;
                }
                if (e.Length > 0) error = $"{p}: {e}";
            }
            catch { /* bỏ qua cổng đang bị chiếm */ }
        }
        return false;
    }

    public void Disconnect()
    {
        _stop = true;
        try { _reader?.Join(600); } catch { }
        _reader = null;
        try { _port?.Close(); } catch { }
        _port?.Dispose();
        _port = null;
        Firmware = "";
        BoardId = "";
        while (_replies.TryTake(out _)) { }
    }

    /// <summary>Gửi một lệnh, đợi phản hồi. Trả về Reply; Ok=false nếu timeout hoặc Board trả về "ERR".</summary>
    public Reply SendCommand(string command, int timeoutMs = 1500)
    {
        SerialPort? port = _port;
        if (port is null || !port.IsOpen)
            return new Reply(false, "", "[chưa kết nối]");

        while (_replies.TryTake(out _)) { }   // bỏ phản hồi cũ còn sót

        try
        {
            port.Write(command + "\n");
        }
        catch (Exception ex)
        {
            return new Reply(false, "", $"[ghi lỗi] {ex.Message}");
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            int wait = (int)Math.Max(10, (deadline - DateTime.UtcNow).TotalMilliseconds);
            if (_replies.TryTake(out string? line, Math.Min(wait, 200)) && line is not null)
                return ParseReply(line);
        }
        return new Reply(false, "", $"[timeout] {command}");
    }

    /// <summary>Gửi một lệnh giao thức cũ (*R, *PASS#, *FAIL#) và đợi phản hồi "OK ...".</summary>
    public Reply Send(string command, int timeoutMs = 1500) => SendCommand(command, timeoutMs);

    private static Reply ParseReply(string line)
    {
        if (line.StartsWith("OK", StringComparison.Ordinal))
            return new Reply(true, line.Length > 3 ? line[3..].Trim() : "", line);
        if (line.StartsWith("ERR", StringComparison.Ordinal))
            return new Reply(false, line.Length > 4 ? line[4..].Trim() : "", line);
        return new Reply(false, line.Trim(), line);
    }

    /// <summary>Đọc một khóa "KEY=VALUE" hoặc "KEY VALUE" trong phản hồi.</summary>
    public static string GetField(string text, string key)
    {
        Match m = Regex.Match(text, $@"(?:^|\s){Regex.Escape(key)}=([^\s]+)");
        if (!m.Success) m = Regex.Match(text, $@"(?:^|\s){Regex.Escape(key)}\s+(\S+)");
        return m.Success ? m.Groups[1].Value : "";
    }

    /// <summary>Đọc giá trị ADC thật từ 4 kênh cảm biến của Board.</summary>
    public MeasResult? ReadMeasurements(int timeoutMs = 1500)
    {
        Reply r = SendCommand("MEAS?", timeoutMs);
        if (!r.Ok) return null;
        if (!int.TryParse(GetField(r.Text, "ACW"), out int acw)) return null;
        int.TryParse(GetField(r.Text, "LEAK"), out int leak);
        int.TryParse(GetField(r.Text, "IR"),   out int ir);
        int.TryParse(GetField(r.Text, "GB"),   out int gb);
        int.TryParse(GetField(r.Text, "VCC_MV"), out int vcc);
        return new MeasResult(acw, leak, ir, gb, vcc);
    }

    public readonly record struct MeasResult(int Acw, int Leak, int Ir, int Gb, int VccMv);

    private void ReadLoop()
    {
        SerialPort? port = _port;
        if (port is null) return;
        while (!_stop)
        {
            string line;
            try { line = port.ReadLine(); }
            catch (TimeoutException) { continue; }
            catch { break; }                       // cổng bị đóng / bị gỡ
            if (string.IsNullOrWhiteSpace(line)) continue;

            line = line.Trim();
            RawLineReceived?.Invoke(line);

            if (line.StartsWith("EVT", StringComparison.Ordinal))
            {
                EventReceived?.Invoke(line);
                continue;
            }
            // "OK BOOT ..." là banner khi board reset, bỏ qua nếu đang đợi phản hồi lệnh
            if (line.StartsWith("OK BOOT", StringComparison.Ordinal))
            {
                EventReceived?.Invoke(line);
                continue;
            }
            if (line.StartsWith("OK", StringComparison.Ordinal) || line.StartsWith("ERR", StringComparison.Ordinal))
            {
                _replies.Add(line);
            }
        }
    }

    public void Dispose() => Disconnect();
}
