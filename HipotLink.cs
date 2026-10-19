using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace GPT9000_WinForms_TestSystem;

/// <summary>Kết quả một phép thử đọc từ GPT-9801 qua lệnh MEAS?.</summary>
public sealed record HipotResult(double VoltageKv, double CurrentMa, double TimeSec,
                                 string Result, string Raw)
{
    public bool IsPass => Result.Equals("PASS", StringComparison.OrdinalIgnoreCase);
    public bool IsError => Result.Equals("ERROR", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Kết nối thật tới máy hipot GW Instek GPT-9800 series (GPT-9801...) qua RS-232.
///
/// Giao thức SCPI, kết thúc dòng bằng '\n'. Lệnh chính (trích từ Hipot_GPT9801.exe):
///   *IDN?                        -> "...GPT-9801..."
///   *CLS                         -> xóa status
///   MANU:NAME &lt;tên&gt;             MANU:RTIM &lt;ramp&gt;      MANU:ACW:FREQ &lt;Hz&gt;
///   MANU:ACW:TTIM &lt;time&gt;         MANU:ACW:REF &lt;mA&gt;      MANU:ACW:CHIS &lt;mA&gt;
///   MANU:ACW:CLOS &lt;mA&gt;
///   MANU:STEP 001; :MANU:STEP?   -> chọn step
///   TEST:RET ON;  :TEST:RET?     -> cho phép máy trả kết quả về PC
///   FUNC:TEST ON;  :FUNC:TEST?   -> BẮT ĐẦU phép thử
///   FUNC:TEST OFF :FUNC:TEST?    -> DỪNG phép thử
///   MEAS?                        -> ví dụ "OK,0.500kV,0.000mA,2.000s,PASS"
///
/// Lớp này giữ một thread đọc nền, tách phản hồi theo yêu cầu (Query) và
/// sự kiện unsolicited (EventReceived) để UI bắt được "InterLock" / "ERROR".
/// </summary>
public sealed class HipotLink : IDisposable
{
    public sealed record Reply(bool Ok, string Text, string Raw);

    /// <summary>Dòng máy đẩy lên không theo yêu cầu (vd. "InterLock", "ERROR").</summary>
    public event Action<string>? EventReceived;

    /// <summary>Mọi dòng thô nhận được - dùng để ghi log chi tiết.</summary>
    public event Action<string>? RawLineReceived;

    private SerialPort? _port;
    private Thread? _reader;
    private volatile bool _stop;
    private readonly BlockingCollection<string> _lines = new(new ConcurrentQueue<string>());

    public bool IsOpen => _port?.IsOpen == true;
    public string? PortName => _port?.PortName;
    public string Idn { get; private set; } = "";

    public static string[] GetPorts() => SerialPort.GetPortNames();

    /// <summary>
    /// Kết nối tới cổng cụ thể. Trả về true nếu máy trả lời *IDN? và chuỗi nhận
    /// được chứa "GPT-98" (chỉ chấp nhận máy GPT-9800 series).
    /// </summary>
    public bool Connect(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits,
                        out string error, int timeoutMs = 3000)
    {
        Disconnect();
        error = "";
        try
        {
            var port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                NewLine = "\n",
                ReadTimeout = 250,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };
            port.Open();
            _port = port;
            _stop = false;
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = $"hipot-{portName}" };
            _reader.Start();

            Thread.Sleep(300);            // chờ line settle
            Drain();

            Reply idn = Query("*IDN?", timeoutMs);
            if (!idn.Ok || idn.Raw.Length == 0)
            {
                error = idn.Raw.Length == 0 ? $"Không có phản hồi *IDN? từ {portName}" : idn.Raw;
                Disconnect();
                return false;
            }
            Idn = idn.Raw.Trim();
            if (!Idn.Contains("GPT-98", StringComparison.OrdinalIgnoreCase))
            {
                error = $"{portName} trả lời nhưng không phải GPT-9800 series: {Idn}";
                Disconnect();
                return false;
            }

            Send("*CLS");
            Thread.Sleep(50);
            Drain();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Disconnect();
            return false;
        }
    }

    /// <summary>Quét các cổng, trả về cổng đầu tiên có máy GPT-9800 trả lời *IDN?.</summary>
    public bool AutoConnect(int baudRate, Parity parity, int dataBits, StopBits stopBits,
                            out string portName, out string error, int timeoutMs = 2500)
    {
        portName = "";
        error = "Không tìm thấy máy GPT-9800 trên cổng serial nào.";
        foreach (string p in GetPorts())
        {
            try
            {
                if (Connect(p, baudRate, parity, dataBits, stopBits, out string e, timeoutMs))
                {
                    portName = p;
                    error = "";
                    return true;
                }
                if (e.Length > 0) error = $"{p}: {e}";
            }
            catch { /* cổng đang bị chiếm - bỏ qua */ }
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
        Idn = "";
        while (_lines.TryTake(out _)) { }
    }

    /// <summary>Gửi lệnh, KHÔNG đợi phản hồi (dùng cho các lệnh setter của SCPI).</summary>
    public bool Send(string command, out string error)
    {
        error = "";
        SerialPort? port = _port;
        if (port is null || !port.IsOpen) { error = "[chưa kết nối]"; return false; }
        try
        {
            port.Write(command + "\n");
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    public void Send(string command) => Send(command, out _);

    /// <summary>Gửi lệnh và đợi dòng phản hồi đầu tiên.</summary>
    public Reply Query(string command, int timeoutMs = 2000)
    {
        if (!Send(command, out string err)) return new Reply(false, "", err);
        return WaitFor(_ => true, timeoutMs);
    }

    /// <summary>Đợi một dòng thỏa mãn predicate. Trả về false nếu timeout.</summary>
    public bool WaitFor(Func<string, bool> predicate, int timeoutMs, out string line)
    {
        line = "";
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            int wait = (int)Math.Max(10, (deadline - DateTime.UtcNow).TotalMilliseconds);
            if (_lines.TryTake(out string? l, Math.Min(wait, 100)) && l is not null)
            {
                if (predicate(l)) { line = l; return true; }
            }
        }
        return false;
    }

    private Reply WaitFor(Func<string, bool> predicate, int timeoutMs)
        => WaitFor(predicate, timeoutMs, out string line)
            ? new Reply(true, line, line)
            : new Reply(false, "", "[timeout]");

    public void Drain()
    {
        while (_lines.TryTake(out _)) { }
    }

    /// <summary>Gửi bộ tham số phép thử từ file chương trình lên máy.</summary>
    public bool SetProgram(TestProgram p, out string error)
    {
        error = "";
        Drain();
        Send("*CLS");
        Thread.Sleep(50);

        string[] cmds =
        {
            $"MANU:NAME {p.Name}",
            $"MANU:RTIM {F(p.RampSec)}",
            $"MANU:ACW:FREQ {F(p.FreqHz)}",
            $"MANU:ACW:TTIM {F(p.TimeSec)}",
            $"MANU:ACW:REF {F(p.RefMa)}",
            $"MANU:ACW:CHIS {F(p.HighSetMa)}",
            $"MANU:ACW:CLOS {F(p.LowSetMa)}"
        };

        foreach (string c in cmds)
        {
            if (!Send(c, out error)) { error = $"{c} -> {error}"; return false; }
            Thread.Sleep(60);            // cho máy xử lý
        }

        Reply step = Query("MANU:STEP 001;:MANU:STEP?", 1500);
        if (!step.Ok) { error = $"MANU:STEP không phản hồi: {step.Raw}"; return false; }

        Reply ret = Query("TEST:RET ON;:TEST:RET?", 1500);
        if (!ret.Ok) { error = $"TEST:RET không phản hồi: {ret.Raw}"; return false; }

        return true;
    }

    /// <summary>Bắt đầu phép thử trên máy.</summary>
    public bool StartTest(out string error)
    {
        error = "";
        Drain();
        if (!Send("*CLS", out error)) return false;
        Thread.Sleep(50);
        Reply r = Query("FUNC:TEST ON;:FUNC:TEST?", 2000);
        if (!r.Ok) { error = $"FUNC:TEST không phản hồi: {r.Raw}"; return false; }
        return true;
    }

    /// <summary>Dừng phép thử (dừng phát áp ngay).</summary>
    public bool StopTest(out string error)
    {
        error = "";
        Drain();
        Send("*CLS");
        Thread.Sleep(50);
        Send("FUNC:TEST OFF:FUNC:TEST?");
        return true;
    }

    /// <summary>
    /// Chờ máy báo phép thử xong (TEST:RET ON -> máy đẩy "OK" khi hoàn tất).
    /// </summary>
    public bool WaitForCompletion(int timeoutMs, out string line)
        => WaitFor(l => l.Contains("OK", StringComparison.OrdinalIgnoreCase), timeoutMs, out line);

    /// <summary>Đọc kết quả thật từ máy bằng lệnh MEAS?.</summary>
    public HipotResult? ReadMeasurement(int timeoutMs = 3000)
    {
        Drain();
        if (!Send("MEAS?", out _)) return null;
        if (!WaitFor(l => l.Contains("mA", StringComparison.OrdinalIgnoreCase) ||
                          l.Contains("kV", StringComparison.OrdinalIgnoreCase) ||
                          l.Contains("PASS", StringComparison.OrdinalIgnoreCase) ||
                          l.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                          l.Contains("ERROR", StringComparison.OrdinalIgnoreCase),
                     timeoutMs, out string line))
            return null;
        return ParseResult(line);
    }

    /// <summary>Định dạng số theo dấu chấm thập phân (máy không hiểu dấu phẩy).</summary>
    private static string F(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Parse câu trả lời của MEAS? thành giá trị thật.
    ///
    /// Máy có thể trả nhiều định dạng khác nhau tùy firmware, ví dụ:
    ///   OK,0.500kV,0.000mA,2.000s,PASS
    ///   OK,=0.500kV,=0.000mA,=2.000S,PASS
    ///   OK 0.500 kV 0.000 mA 2.000 s PASS
    /// Vì vậy parser dựa trên ĐƠN VỊ (kV / mA / s) chứ không theo vị trí cột,
    /// và có fallback về cách cắt chuỗi cổ điển nếu không khớp regex.
    /// </summary>
    public static HipotResult ParseResult(string raw)
    {
        string s = raw.Trim();
        string result = "UNKNOWN";

        if (s.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) result = "ERROR";
        else if (s.Contains("PASS", StringComparison.OrdinalIgnoreCase)) result = "PASS";
        else if (s.Contains("FAIL", StringComparison.OrdinalIgnoreCase)) result = "FAIL";
        else if (s.Contains("OVP", StringComparison.OrdinalIgnoreCase) ||
                 s.Contains("OCP", StringComparison.OrdinalIgnoreCase) ||
                 s.Contains("OT", StringComparison.OrdinalIgnoreCase)) result = "ERROR";

        double? kv = FindValue(s, @"([-+]?\d*\.?\d+)\s*kV");
        double? ma = FindValue(s, @"([-+]?\d*\.?\d+)\s*mA");
        double? sec = FindValue(s, @"([-+]?\d*\.?\d+)\s*(?:s\b|sec\b|secs\b)");

        // Fallback cổ điển của Hipot_GPT9801: cắt 5 ký tự quanh "kV" và "=".
        if (kv is null || ma is null)
        {
            try
            {
                int ikv = s.IndexOf("kV", StringComparison.OrdinalIgnoreCase);
                if (ikv >= 5 && kv is null)
                    kv = ParseNum(s.Substring(ikv - 5, 5));
                if (ikv >= 0 && ma is null)
                {
                    int start = ikv + 2;
                    while (start < s.Length && (s[start] == ',' || s[start] == ' ' || s[start] == '=')) start++;
                    ma = ParseNum(s.Substring(start, Math.Min(5, s.Length - start)));
                }
                int ieq = s.IndexOf('=');
                if (ieq >= 0 && sec is null)
                    sec = ParseNum(s.Substring(ieq + 1, Math.Min(5, s.Length - ieq - 1)));
            }
            catch { /* bỏ qua - giữ null */ }
        }

        // "UNKNOWN" nhưng có mA thì coi như FAIL để không âm thầm bỏ qua.
        if (result == "UNKNOWN" && ma.HasValue) result = "FAIL";

        return new HipotResult(kv ?? 0, ma ?? 0, sec ?? 0, result, s);
    }

    private static double? FindValue(string s, string pattern)
    {
        Match m = Regex.Match(s, pattern, RegexOptions.IgnoreCase);
        return m.Success && double.TryParse(m.Groups[1].Value,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
            out double v) ? v : null;
    }

    private static double? ParseNum(string fragment)
    {
        Match m = Regex.Match(fragment, @"[-+]?\d*\.?\d+");
        return m.Success && double.TryParse(m.Value,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
            out double v) ? v : null;
    }

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

            // Máy đẩy sự kiện không theo yêu cầu -> đưa lên UI. Vẫn GIỮ dòng trong hàng đợi
            // phản hồi để không làm mất câu trả lời của một Query đang chờ.
            if (line.Contains("InterLock", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                EventReceived?.Invoke(line);
            }

            _lines.Add(line);
        }
    }

    public void Dispose() => Disconnect();
}
