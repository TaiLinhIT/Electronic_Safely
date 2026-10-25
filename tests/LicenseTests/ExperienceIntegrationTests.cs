using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using GPT9000_WinForms_TestSystem;
using LicenseShared;

internal static partial class Program
{
    private static void RunExperienceChecks(string fingerprint, byte[] secret, RSA rsa, string keyId, string bundle)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Experience test +7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
        DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(7));
        DateTimeOffset expiry = At(23, 16);
        var windows = ExtraExperience.Build(new(2026, 10, 23), new(16, 0), zone,
            new[] { 0 }, new[] { new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26) });
        string dir = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "office.dat");
        var service = new OfficeLicenseService(path, "TEST", Convert.ToBase64String(secret), bundle);
        LicensePayload Payload() => new()
        {
            ProductId = "TEST", DeviceId = fingerprint, KeyId = keyId, LicenseType = LicenseType.Custom,
            IssuedAtUtc = At(20, 0), NotBeforeUtc = At(20, 0), ExpiresAtUtc = expiry,
            ExtraWindows = windows.ToList()
        };
        byte[] Pack(byte[] bytes, byte[]? signature = null)
        {
            signature ??= rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            return OfficeLicenseFile.Create(LicenseCrypto.SerializeEnvelope(new SignedLicense
            {
                Algorithm = "RSA-2048-PSS-SHA256", KeyId = keyId,
                PayloadBase64 = Convert.ToBase64String(bytes), SignatureBase64 = Convert.ToBase64String(signature)
            }), "TEST", fingerprint, secret);
        }
        byte[] Seal(LicensePayload payload) => Pack(LicenseCodec.SerializePayload(payload));
        byte[] encoded = LicenseCodec.SerializePayload(Payload());
        byte[] valid = Pack(encoded);
        Check(encoded[0] == 2 && LicenseCodec.DeserializePayload(encoded).ExtraWindows.SequenceEqual(windows),
            "v2 round-trips the exact signed UTC windows");
        Check(service.Validate(valid, At(23, 15)), "v2 main validity works outside extra windows");
        Check(service.Validate(valid, expiry), "v2 main expiry instant remains inclusive");
        Check(!service.Validate(valid, expiry.AddTicks(1), out bool expired) && expired,
            "gap immediately after main expiry is denied as expired");
        Check(!service.Validate(valid, At(24, 16).AddTicks(-1)), "extra window rejects one tick before its start");
        Check(service.Validate(valid, At(24, 16)), "extra window includes its exact start");
        Check(service.Validate(valid, At(24, 17).AddTicks(-1)), "extra window includes last tick before end");
        Check(!service.Validate(valid, At(24, 17)), "extra window excludes its exact end");
        Check(!service.Validate(valid, At(25, 16, 30)), "unselected day between valid windows is denied");
        Check(service.Validate(valid, At(26, 16, 30)), "later selected day opens again");
        Check(!service.Validate(valid, At(26, 17), out expired) && expired, "final extra-window end denies START");
        Check(service.Validate(valid, At(24, 16, 30).ToOffset(TimeSpan.FromHours(-5))),
            "client timezone representation does not change signed UTC permission");
        Check(!service.Validate(valid, Payload().NotBeforeUtc.AddTicks(-1)), "v2 keeps strict not-before to the tick");

        var p = Payload(); p.ExtraWindows.Clear();
        byte[] v1 = LicenseCodec.SerializePayload(p);
        Check(v1[0] == 1 && LicenseCodec.DeserializePayload(v1).ExtraWindows.Count == 0, "no extra schedule still writes v1");
        Check(service.Validate(Pack(v1), expiry) && !service.Validate(Pack(v1), expiry.AddTicks(1)), "v1 validity boundaries remain unchanged");
        Check(!service.Validate(Pack(v1), p.NotBeforeUtc.AddTicks(-1)), "v1 not-before remains strict");
        p = Payload();
        p.ExtraWindows = ExtraExperience.Build(new(2026, 10, 23), new(16, 0), zone, new[] { 0, 23 }, Array.Empty<DateOnly>());
        Check(service.Validate(Seal(p), At(24, 15, 30)) && !service.Validate(Seal(p), At(24, 16)),
            "hour-only extension ends after 24 hours without daily repetition");
        p.ExtraWindows = ExtraExperience.Build(new(2026, 10, 23), new(16, 0), zone, Array.Empty<int>(), new[] { new DateOnly(2026, 10, 24) });
        Check(service.Validate(Seal(p), At(24, 0)) && service.Validate(Seal(p), At(24, 23, 59)) && !service.Validate(Seal(p), At(25, 0)),
            "day-only extension includes selected full day and excludes next midnight");
        p.ExpiresAtUtc = At(23, 16, 30);
        p.ExtraWindows = ExtraExperience.Build(new(2026, 10, 23), new(16, 30), zone, new[] { 7 }, new[] { new DateOnly(2026, 10, 24) });
        byte[] partial = Seal(p);
        Check(service.Validate(partial, At(24, 0, 15)) && service.Validate(partial, At(24, 23, 45)) &&
              !service.Validate(partial, At(24, 0, 30)) && !service.Validate(partial, At(25, 0, 15)),
              "midnight-crossing extension cannot spill into an unselected day");
        p.ExpiresAtUtc = new DateTimeOffset(2028, 1, 31, 16, 0, 0, TimeSpan.FromHours(7));
        p.NotBeforeUtc = p.ExpiresAtUtc.AddDays(-1);
        p.ExtraWindows = ExtraExperience.Build(new(2028, 1, 31), new(16, 0), zone, Array.Empty<int>(), new[] { new DateOnly(2028, 2, 29) });
        Check(service.Validate(Seal(p), new DateTimeOffset(2028, 2, 29, 12, 0, 0, TimeSpan.FromHours(7))),
            "leap-year last day of month is supported end to end");

        p = Payload(); p.ProductId = "OTHER";
        Check(!service.Validate(Seal(p), At(24, 16, 30), out expired) && !expired, "extra windows never bypass product binding");
        p = Payload(); p.DeviceId = new string('0', 64);
        Check(!service.Validate(Seal(p), At(24, 16, 30), out expired) && !expired, "extra windows never bypass machine binding");
        var wrongKey = new OfficeLicenseService(path, "TEST", Convert.ToBase64String(secret), "{}");
        Check(!wrongKey.Validate(valid, At(24, 16, 30)), "extra windows still require a trusted signing key");
        var wrongSecret = new OfficeLicenseService(path, "TEST", Convert.ToBase64String(new byte[32]), bundle);
        Check(!wrongSecret.Validate(valid, At(24, 16, 30)), "extra windows still require the correct encryption secret");
        byte[] signature = rsa.SignData(encoded, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        byte[] altered = (byte[])encoded.Clone(); altered[^1] ^= 1;
        Check(!service.Validate(Pack(altered, signature), At(24, 16, 30)), "editing signed extra-window bytes invalidates the signature");
        byte[] badTag = (byte[])valid.Clone(); badTag[^1] ^= 1;
        Check(!service.Validate(badTag, At(24, 16, 30), out expired) && !expired, "tampered GCM tag cannot masquerade as expiry");

        byte[] RawSchedule(LicensePayload payload, ushort count, params (long Start, long End)[] intervals)
        {
            payload.ExtraWindows.Clear();
            byte[] prefix = LicenseCodec.SerializePayload(payload);
            prefix[0] = 2;
            using var stream = new MemoryStream();
            stream.Write(prefix);
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(count);
                foreach (var interval in intervals) { writer.Write(interval.Start); writer.Write(interval.End); }
            }
            return stream.ToArray();
        }
        void Reject(string name, byte[] bytes)
            => Check(!service.Validate(Pack(bytes), At(24, 16, 30), out bool isExpired) && !isExpired, name);
        long start = At(24, 16).UtcTicks, end = At(24, 17).UtcTicks;
        Reject("signed v2 with zero windows rejected", RawSchedule(Payload(), 0));
        Reject("signed v2 with over 800 windows rejected", RawSchedule(Payload(), 801));
        Reject("empty interval rejected", RawSchedule(Payload(), 1, (start, start)));
        Reject("negative interval rejected", RawSchedule(Payload(), 1, (end, start)));
        Reject("window before main expiry rejected", RawSchedule(Payload(), 1, (expiry.AddTicks(-1).UtcTicks, end)));
        Reject("overlapping intervals rejected", RawSchedule(Payload(), 2, (start, end), (start + 1, end + 1)));
        Reject("unsorted intervals rejected", RawSchedule(Payload(), 2, (start + 2, end + 2), (start, end)));
        Reject("window beyond 33-day limit rejected", RawSchedule(Payload(), 1, (start, expiry.AddDays(33).AddTicks(1).UtcTicks)));
        Reject("invalid UTC ticks rejected without crash", RawSchedule(Payload(), 1, (-1, long.MaxValue)));
        p = Payload(); p.LicenseType = LicenseType.Perpetual;
        Reject("perpetual payload cannot carry extra windows", RawSchedule(p, 1, (start, end)));
        Reject("truncated v2 payload rejected", encoded[..^1]);
        Reject("trailing payload bytes rejected", encoded.Concat(new byte[] { 0 }).ToArray());
        altered = (byte[])encoded.Clone(); altered[0] = 3;
        Reject("unsupported payload version rejected", altered);
        p = Payload(); p.LicenseType = (LicenseType)255;
        Reject("unknown v2 license type rejected", LicenseCodec.SerializePayload(p));
        p = Payload();
        p.ExtraWindows = Enumerable.Range(0, 800).Select(i => new ExperienceWindow(
            expiry.AddMinutes(i * 2 + 1), expiry.AddMinutes(i * 2 + 2))).ToList();
        Check(service.Validate(Seal(p), expiry.AddSeconds(90)), "maximum 800 signed windows accepted");

        // Reproduce the reported 20:30-21:30 case: main expiry is Oct 24,
        // while the issuer selected extra DAYS Oct 25, Oct 27 and Nov 4.
        p = Payload(); p.NotBeforeUtc = At(24, 0); p.ExpiresAtUtc = At(24, 20, 30);
        p.ExtraWindows = ExtraExperience.Build(new(2026, 10, 24), new(20, 30), zone,
            new[] { 0, 2, 11, 12, 17, 20 },
            new[] { new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 27), new DateOnly(2026, 11, 4) });
        byte[] reported = Seal(p);
        foreach (var sample in new[]
        {
            (At(24, 20, 30).AddSeconds(-1), true, "main validity before 20:30 on Oct 24"),
            (At(24, 20, 30), true, "inclusive main deadline on Oct 24"),
            (At(24, 20, 30).AddTicks(1), false, "no extra day selected on Oct 24"),
            (At(24, 21, 30), false, "21:30 on expiry day remains outside the schedule"),
            (At(25, 20, 30).AddTicks(-1), false, "next selected day stays closed before 20:30"),
            (At(25, 20, 30), true, "same service reopens exactly at 20:30 on Oct 25"),
            (At(25, 21, 30).AddTicks(-1), true, "20:30-21:30 window stays open until its last tick"),
            (At(25, 21, 30), false, "selected window closes exactly at 21:30"),
            (At(26, 20, 45), false, "unselected Oct 26 cannot reuse the daily clock interval"),
            (At(27, 20, 30), true, "same service reopens at the next selected day"),
            (new DateTimeOffset(2026, 11, 4, 20, 30, 0, TimeSpan.FromHours(7)), true, "November selected day opens normally")
        })
            Check(service.Validate(reported, sample.Item1) == sample.Item2, "reported schedule: " + sample.Item3);
        p.ExtraWindows = ExtraExperience.Build(new(2026, 10, 24), new(20, 30), zone,
            new[] { 0 }, Array.Empty<DateOnly>());
        byte[] sameDay = Seal(p);
        Check(service.Validate(sameDay, At(24, 20, 45)) && !service.Validate(sameDay, At(24, 21, 30)),
            "hour-only selection grants 20:30-21:30 immediately on Oct 24");

        var now = DateTimeOffset.UtcNow;
        p = Payload(); p.NotBeforeUtc = now.AddDays(-1); p.IssuedAtUtc = p.NotBeforeUtc;
        p.ExpiresAtUtc = now.AddMinutes(-20);
        p.ExtraWindows = new() { new(p.ExpiresAtUtc.AddMinutes(5), p.ExpiresAtUtc.AddMinutes(10)) };
        byte[] ended = Seal(p);
        File.WriteAllBytes(path, ended);
        Check(!service.CanStartTest(out expired) && expired, "file check reports expiry after main and extra validity end");
        p.ExtraWindows = new() { new(now.AddMinutes(-1), now.AddMinutes(5)) };
        File.WriteAllBytes(path, Seal(p));
        Check(service.CanStartTest(out expired) && !expired, "replacement file activates an extra window without restarting");
        p.ExtraWindows.Clear(); p.ExpiresAtUtc = now.AddDays(1);
        File.WriteAllBytes(path, Seal(p));
        Check(service.CanStartTest(), "renewed v1 file remains compatible after v2");
        File.WriteAllBytes(path, ended);
        TestExpiredStartUi(service, wrongSecret);
    }

    private static void TestExpiredStartUi(OfficeLicenseService expiredLicense, OfficeLicenseService invalidLicense)
    {
        using var form = new StartErrorTestForm();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object Field(string name) => typeof(MainForm).GetField(name, flags)!.GetValue(form)!;
        void Set(string name, object value) => typeof(MainForm).GetField(name, flags)!.SetValue(form, value);
        void Call(string name, params object[] args) => typeof(MainForm).GetMethod(name, flags)!.Invoke(form, args);
        Set("_license", expiredLicense);
        var grid = (DataGridView)Field("_grid");
        var stats = (TestStats)Field("_stats");
        try
        {
            foreach (var source in new[] { "screen", "F5", "board" })
            {
                int before = form.Notices.Count;
                if (source == "screen") Call("StartTest", false);
                else if (source == "F5") Check(form.PressF5(), "F5 uses the common START gate");
                else Call("OnBoardLine", "*TESTON#");
                Check(form.Notices.Count == before + 1 && form.Notices[^1] ==
                      "Lỗi phần cứng thiết bị. Vui lòng liên hệ bộ phận kỹ thuật để được hỗ trợ.",
                      "expired " + source + " START shows the requested device-error message");
                Check(!(bool)Field("_running") && grid.Rows.Count == 0 && stats.Total == 0,
                      "expired " + source + " START neither runs nor records a failed measurement");
            }
            Check(((Label)Field("_status")).Text == "LỖI THIẾT BỊ", "expired START replaces a retained result with the blocked-start status");
            int count = form.Notices.Count;
            Set("_running", true);
            Call("StartTest", false);
            Check((bool)Field("_running") && form.Notices.Count == count, "expiry does not interrupt an already running measurement");
            Call("RequestStop", false);
            Check((bool)Field("_stopRequested"), "STOP remains available after expiry");
            Set("_running", false);
            form.OnNotice = () => Call("OnBoardLine", "*TESTON#");
            Call("StartTest", false);
            Check(form.Notices.Count == count + 1, "repeated board START cannot stack expiry dialogs while one is open");
            form.OnNotice = null;
            Set("_license", invalidLicense);
            count = form.Notices.Count;
            Call("StartTest", false);
            Check(form.Notices.Count == count && !(bool)Field("_running"), "invalid signatures/keys retain fail-closed silent rejection");
        }
        finally { Call("Shutdown"); }
    }

    private sealed class StartErrorTestForm : MainForm
    {
        public List<string> Notices { get; } = new();
        public Action? OnNotice { get; set; }
        protected override void ShowStartError(string message) { Notices.Add(message); OnNotice?.Invoke(); }
        public bool PressF5() { Message message = default; return base.ProcessCmdKey(ref message, Keys.F5); }
    }
}
