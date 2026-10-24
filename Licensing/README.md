# office.dat integration

The client opens normally without a license. Every START (screen, F5, or board)
reads the configured `OfficeDatPath` in `Config\SystemSetting.xml` again (default:
`C:\Program Files\offices\office.dat`). Root can browse for an existing office.dat
with the Windows file picker and save its path. The file is used in place, not copied;
Cancel leaves the selection unchanged. Missing, locked, corrupt,
wrong-machine, invalid-signature, not-yet-valid and expired files silently deny START.
No failed measurement/report is created. Replace the file to renew without restarting.
An already running measurement retains its normal stop and completion handling.

A physical START may close the fixture before the PC receives `*TESTON#`.
When denied, the PC sends `TEST ABORT` to release it and never sends `HV ARM`
or `FUNC:TEST ON`. Preventing the board's initial mechanical movement would require
a separate firmware change. Emergency-stop handling is not gated by licensing.

Settings passwords: `123456` enables editing; `root123456` additionally shows the
office.dat path and folder button. Saving successfully, locking, or leaving the
settings tab removes both permissions and hides the license controls. Invalid or
expired licenses do not prevent entering settings to replace the file.

## Build provisioning

Place these files in `Licensing/Provisioning/` before building a customer binary:

- `product-id.txt`: the exact issuer ProductId (`GPT9000_WinForms_TestSystem`).
- `office-secret.txt`: the 32-byte vendor secret as Base64.
- `public-keys.json`: Warehouse License Studio's public-key bundle (include keys
  used for older licenses when renewing with a different signing key).

These are embedded resources, excluded from Git and not read from customer-editable
files at runtime. Never include a private signing key. Keep a secure backup of the
vendor material; a fresh checkout needs these files restored before building.
An unprovisioned debug build denies every START; Release builds require provisioning.

`Shared/` is a snapshot of the matching source in `Authentication/LicenseShared`
(2026-10-23), so this project builds without an external project or DLL dependency.
Verification covers OFFC1 AES-GCM/HKDF, WLC-1 signed binary payload, trusted public
key, ProductId, local machine binding and UTC dates. RSA-PSS and ECDSA are supported.
Legacy licenses with a hyphenated/colon-separated MAC in ManualDeviceId are accepted
only if the signed address matches an actual local adapter. New MacAddress bindings,
full fingerprints and DEV IDs are also supported. The expiry endpoint is inclusive;
not-before has no early tolerance. Clock rollback protection is not implemented.

Run the isolated tests without connecting physical equipment:

```powershell
dotnet run --project tests/LicenseTests/LicenseTests.csproj
```

An optional office.dat path checks the real export against the embedded vendor keys
and this machine, without changing or issuing a license.
