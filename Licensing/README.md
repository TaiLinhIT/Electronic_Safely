# office.dat integration

The client opens normally without a license. Every START (screen, F5, or board)
reads `C:\Program Files\offices\office.dat` again and verifies the encrypted file,
trusted signature, product, machine and current UTC validity. Missing, locked,
corrupt, wrong-machine, invalid-signature and not-yet-valid files deny START silently.
When a verified license has passed its main expiry and the current time is outside
all signed extra windows, START is blocked and the operator sees:

`Lỗi phần cứng thiết bị. Vui lòng liên hệ bộ phận kỹ thuật để được hỗ trợ.`

The result panel shows `LỖI THIẾT BỊ`; no measurement, FAIL count or report is created.
Replace office.dat to renew without restarting. An already running measurement retains
its normal stop and completion handling; expiry only blocks the next START.

A physical START may close the fixture before the PC receives `*TESTON#`.
When denied, the PC sends `TEST ABORT` to release it before showing any dialog and
never sends `HV ARM` or `FUNC:TEST ON`. Preventing the board's initial mechanical movement would require
a separate firmware change. Emergency-stop handling is not gated by licensing.

Settings passwords: `123456` enables editing; `root123456` additionally shows the
office.dat path and folder button. Saving successfully, locking, or leaving the
settings tab removes both permissions and hides the license controls. Invalid or
expired licenses do not prevent entering settings to replace the file.

## Build provisioning

Place these files in `Licensing/Provisioning/` before building a customer binary:

- `product-id.txt`: the exact issuer ProductId (`Electronic-Safely` in the current provisioning).
- `office-secret.txt`: the 32-byte vendor secret as Base64.
- `public-keys.json`: Warehouse License Studio's public-key bundle (include keys
  used for older licenses when renewing with a different signing key).

These are embedded resources, excluded from Git and not read from customer-editable
files at runtime. Never include a private signing key. Keep a secure backup of the
vendor material; a fresh checkout needs these files restored before building.
An unprovisioned debug build denies every START; Release builds require provisioning.

`Shared/` is a snapshot of the matching source in `Authentication/LicenseShared`
(2026-10-24): the original eight files plus `ExtraExperience.cs` and `LicenseDateInput.cs`.
This project builds without an external project or DLL dependency.
Verification covers OFFC1 AES-GCM/HKDF, WLC-1 signed binary payload, trusted public
key, ProductId, local machine binding and UTC dates. RSA-PSS and ECDSA are supported.
Legacy licenses with a hyphenated/colon-separated MAC in ManualDeviceId are accepted
only if the signed address matches an actual local adapter. New MacAddress bindings,
full fingerprints and DEV IDs are also supported. The expiry endpoint is inclusive;
not-before has no early tolerance. Clock rollback protection is not implemented;
the Electronic_Safely integration guide treats ClockGuard as a separate improvement.

## Signed extra experience windows

- OFFC1 file version and WLC-1 envelope remain unchanged. Payload v1 is still accepted.
- Payload v2 carries `ExtraWindows`: up to 800 sorted, non-overlapping UTC intervals.
- The main expiry instant is inclusive. After it, permission requires
  `StartUtc <= nowUtc < EndUtc` in one signed interval. Gaps stay blocked even when
  a later interval exists; the client does not extend expiry to the final interval.
- The issuer builds the local hour/day selections. The client uses only the UTC
  intervals verified by the signature, without rebuilding a schedule from client timezone.
- Empty/reversed/overlapping/out-of-order intervals, intervals before main expiry or
  ending more than 33 days after it, invalid ticks, unsupported payload versions,
  truncated/trailing bytes and perpetual licenses carrying intervals are rejected.
- ProductId, embedded trusted keys and secret were not changed for this upgrade.

Reference: `Authentication/docs/Electronic-Safely-Tich-hop-trai-nghiem-them.md`.

Run the isolated tests without connecting physical equipment:

```powershell
dotnet run --project tests/LicenseTests/LicenseTests.csproj
```

An optional office.dat path checks the real export against the embedded vendor keys
and this machine, without changing or issuing a license.

For read-only diagnostics without running the test suite:

```powershell
dotnet run --project tests/LicenseTests/LicenseTests.csproj -- --inspect "C:\path\office.dat"
```

The output includes the file hash, verified main deadline, extra windows and sample
20:30/21:30 decisions in UTC+7. An optional second argument pointing to the issuer's
`issued-licenses.json` supplies binding candidates when inspecting a file issued to
another PC. These candidates only permit diagnostic decryption; they never change
the application's local-machine START validation. No secret or full device ID is printed.

Selecting extra days means those dates only: a main deadline of Oct 24 at 20:30,
plus Oct 25's 20:30-21:30 window, does not grant the same interval on Oct 24.
For an immediate extra hour on the expiry date, select the first hour and no extra days.

The tests include the issuer's extra-experience cases, signed/encrypted v1/v2 files,
malformed schedules, replacement in the same session and the three START entry points.
The notification endpoint is overridden in UI tests so they do not open dialogs or
start serial communication. These tests do not verify physical fixture/HV operation.
