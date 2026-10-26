# Hệ thống kiểm tra an toàn điện GPT-9000 (WinForms + .NET 8)

## Sửa kết nối và shortcut (26/10/2026)

- Một lần chỉ chạy một lượt kết nối; các nút kết nối, sửa cài đặt và bắt đầu thử được khóa trong lúc dò cổng. Dừng polling và tạm ngắt handler cũ trước khi mở lại thiết bị để không lấy nhầm phản hồi giữa các lượt.
- Nếu cổng máy đo đã đổi, phần mềm dò lại cổng có phản hồi GPT-98 và lưu cổng tìm được. Máy đo và bảng điều khiển không dò vào cổng của nhau. Tự động dò bỏ qua cổng Bluetooth `BthModem`; vẫn chọn được cổng Bluetooth cụ thể nếu hệ thống có dùng.
- Thông báo phân biệt cổng không mở được với cổng đã mở nhưng không có phản hồi `*IDN?`. Cấu hình hiện tại là 115200, 8N1. Trên máy đo: **UTILITY → INTER**, chọn USB nếu cáp USB cắm trực tiếp, hoặc RS232 nếu dùng dây RS-232/bộ chuyển USB–RS232; RS232 đặt Baud 115200, USB cố định 115200. Nhấn **EDIT/SAVE** để lưu. Tham khảo [hướng dẫn GW Instek, trang 103–104 và 117–118](https://www.gwinstek.com/en-global/products/downloadSeriesDownNew/10502/802).
- Trong thư mục bản build có `Tao-shortcut-MiTTECH.ps1`: chạy bằng PowerShell để tạo shortcut **MiT TECH - Kiem tra an toan dien**. Shortcut dùng bản sao icon theo mã nội dung trong hồ sơ người dùng và làm mới thông tin icon của Windows, tránh ảnh HAN cũ còn lưu theo đường dẫn EXE. Không xóa bộ nhớ đệm hay khởi động lại Explorer.

## Cài thời gian chu trình (25/10/2026)

- Tab **Cài đặt** có một nhóm gọn với 4 thời gian chính: **Đóng đồ gá**, **Đẩy kim pogo**, **Rút kim pogo**, **Mở đồ gá**. Các ô được đánh số theo thứ tự chu trình, nhập theo giây, chính xác đến 0,001 giây.
- Các thời gian chờ kỹ thuật dùng giá trị đang lưu và không hiện thành ô chỉnh. Phần mềm tự tăng ngân sách chờ đồ gá về nếu cần để đủ tổng thời gian rút kim + mở đồ gá + 0,5 giây; việc thu gọn giao diện không đặt lại các thông số cũ.
- Mở khóa bằng mật khẩu cài đặt, chỉnh thời gian rồi bấm **Lưu cấu hình**. Cấu hình lưu trong `Config\SystemSetting.xml` cạnh EXE. Nếu đang nối bảng điều khiển, phần mềm gửi và kiểm tra xác nhận; nếu chưa nối, cấu hình được gửi khi kết nối.
- **Lưu và kết nối lại** lưu thông số rồi dò lại thiết bị, phù hợp khi đổi cổng COM. Không đổi thông số giữa chu trình đóng, thử hoặc rút/mở đồ gá.
- Cần nạp **firmware 1.4.0** một lần để chỉnh các thời gian cơ khí. Firmware cũ vẫn chạy với các thời gian mặc định, nhưng không áp dụng được thời gian cơ khí tùy chỉnh.
- Mã firmware và hướng dẫn: [firmware/README.md](firmware/README.md). File HEX cho Uno/Nano ATmega328P: `firmware/build/GPT9800_ControlBoard.hex`.
- Bảng điều khiển khởi động lại sẽ mất cấu hình trong RAM; nhấn **Kết nối lại** để phần mềm gửi lại thông số đã lưu trước khi thử tiếp.

## Cập nhật giao diện MiT TECH (25/10/2026)

- Logo MiT TECH gốc được nhúng trong ứng dụng, hiển thị ở góc phải đầu trang; tên ứng dụng căn giữa, đồng hồ ở bên trái.
- Icon MiT TECH đa kích thước được nhúng vào EXE, cửa sổ chính, cửa sổ chọn/sửa chương trình và đăng nhập để hiển thị đồng nhất trên taskbar; không cần chép logo rời khi phân phối.
- Icon desktop/taskbar dùng bản vuông `Assets/MitTechApp.ico`, phóng lớn chữ MiT TECH và bánh răng, gồm các cỡ 16/20/24/32/40/48/64/96/128/256 px. Tạo lại từ `Assets/MitTechIcon.png` bằng `powershell -File tools/Build-AppIcon.ps1`; prompt ImageGen lưu tại `Assets/MitTechIcon.prompt.md`.
- Khi shortcut Windows còn giữ icon cũ, đặt vị trí icon thành `Assets\MitTechApp.ico` trong thư mục bản build mà shortcut đang chạy.
- Bảng thông tin/trạng thái bên phải giữ bề rộng 350 px ở DPI 100%; kết quả và biểu đồ bên trái dùng toàn bộ phần còn lại.
- Chữ kết quả 14 pt, dòng cao 44 px, tiêu đề cột xuống dòng; chữ, nhãn số và diện tích biểu đồ được tăng kích thước.
- Giao diện, cột kết quả, biểu đồ, trạng thái, tùy chọn kết nối và nút trong hộp thông báo dùng tiếng Việt; giữ `PASS` / `FAIL` cho kết quả đo.
- Bảng kết quả luôn có dòng mới nhất ở đầu, tô nền xanh nhạt; lịch sử cũ nằm bên dưới. Tiêu đề hiển thị `LỊCH SỬ KẾT QUẢ`.
- Sau khi hoàn tất và mở đồ gá, ô `KẾT QUẢ KIỂM TRA` giữ nguyên kết quả/màu cho tới yêu cầu bắt đầu hợp lệ tiếp theo; lúc đó chuyển sang `ĐANG CHUẨN BỊ` rồi `ĐANG KIỂM TRA`.
- Bố cục hỗ trợ DPI hệ thống, cửa sổ thấp có thanh cuộn; log mới không tự kéo cả trang xuống.
- Build: `dotnet build GPT9000_WinForms_TestSystem.csproj -c Release`. File chạy nằm trong `bin\Release\net8.0-windows\`.

## Cập nhật cài đặt và office.dat (23/10/2026)

- `123456`: mở các ô cài đặt. `root123456`: mở các ô cài đặt và hiện đường dẫn/nút mở thư mục `office.dat`.
- Lưu thành công, khóa lại hoặc rời tab Cài đặt sẽ khóa các ô và ẩn phần `office.dat`.
- File nằm tại `C:\Program Files\offices\office.dat`; nút mở thư mục cũng trỏ đến đây.
- App vẫn mở khi thiếu/hết hạn/sai license. Khi đã qua hạn chính và đang ngoài mọi khoảng trải nghiệm thêm đã ký, bấm bắt đầu sẽ bị chặn và hiện “Lỗi phần cứng thiết bị. Vui lòng liên hệ bộ phận kỹ thuật để được hỗ trợ.”
- Hỗ trợ `office.dat` với payload v1 và v2. Trong hạn chính vẫn được chạy bình thường; sau hạn chỉ được chạy đúng khoảng giờ/ngày trải nghiệm đã ký. Không mở quyền cho các khoảng trống giữa lịch.
- Thay `office.dat` hợp lệ để gia hạn; lần bấm Test tiếp theo đọc lại file, không cần khởi động lại.
- Chi tiết build, khóa nhúng và kiểm thử: [Licensing/README.md](Licensing/README.md).

Phần mềm vận hành trạm test an toàn điện với **máy hipot GW Instek GPT-9801 thật**
(kết nối RS-232 / SCPI) và **bảng điều khiển Arduino thật** (giao thức serial ASCII).

> **CẢNH BÁO AN TOÀN**: phần mềm + firmware là lớp kiểm soát vận hành, **không thay thế**
> thiết kế an toàn điện áp cao chuyên nghiệp. Không dùng để điều khiển thiết bị cao áp
> thật mà chưa qua đánh giá an toàn.

---

## 1. Quy trình test (8 bước)

Trạng thái hiện ở ô **TEST RESULT** (nhãn lớn, click được) và dòng *Bước x/8* bên dưới.

1. **Khởi động phần mềm** — đọc `config.ini`, `Config\SystemSetting.xml`, `Config\pw.org`,
   nạp chương trình phép thử từ `program\*.csv`.
2. **Kiểm tra kết nối và trạng thái** — dò và kết nối **GPT-9801** (xác thực bằng `*IDN?`,
   chỉ chấp nhận chuỗi chứa `GPT-98`) và **Control Board** (`PING` → `OK PONG ...`).
   Gửi `*R` cho bảng điều khiển. Nếu thiếu thiết bị → `NO DEV`, nút START bị khóa, hỏi dò lại.
3. **Đặt unit vào fixture** — operator đặt unit, đóng nắp an toàn rồi tick checkbox
   *Unit đã vào fixture && nắp an toàn đã đóng*. Phần mềm hỏi lại board bằng `STATUS?`
   và **từ chối** nếu `INTERLOCK != CLOSED` hoặc `UNIT != PRESENT`.
4. **Nhận START TEST** — từ 3 nguồn: nút **START TEST** trên UI, phím **F5**, hoặc
   **nút START vật lý trên bảng điều khiển** (board gửi `*TESTON#`).
5. **GPT-9801 thực hiện phép thử** (thật):
   - `TEST START` lên board → board tự kiểm interlock/unit rồi đóng relay.
   - `*CLS` + `FUNC:TEST ON;:FUNC:TEST?` → máy phát áp.
   - Chờ máy báo hoàn tất (`OK`) trong thời gian ramp + test + dự phòng.
   - `MEAS?` → đọc **điện áp / dòng / thời gian / PASS-FAIL THẬT** từ máy.
6. **6A. PASS** (xanh) hoặc **6B. FAIL** (đỏ) — gửi `*PASS#` / `*FAIL#` cho board
   để bật đèn + còi.
7. **Lưu kết quả** — ghi báo cáo của ca và thống kê pass rate (xem mục 3).
8. **Chờ unit tiếp theo** — gửi `RESET` cho board, bỏ tick checkbox, về `IDLE`.

**Dừng khẩn cấp**: phím **Esc**, nút **STOP**, nút ABORT trên board, hoặc mở nắp an toàn.
Phần mềm gửi `FUNC:TEST OFF` (cắt phát áp **ngay trên máy**) và `TEST ABORT` cho board.

---

## 2. Các thiết bị thật mà phần mềm kết nối

### 2.1 Máy hipot GPT-9801 — `HipotLink.cs`

Cổng serial đọc từ `Config\SystemSetting.xml` (`ComPortGPT`, mặc định `COM2`,
115200 8N1; đặt `AUTO` để quét toàn bộ cổng).

| Giao thức SCPI đã dùng | Ý nghĩa |
|---|---|
| `*IDN?` | Xác thực máy (phải chứa `GPT-98`) |
| `*CLS` | Xóa trạng thái |
| `MANU:NAME` `MANU:RTIM` `MANU:ACW:FREQ` `MANU:ACW:TTIM` | Nạp tham số chương trình |
| `MANU:ACW:REF` `MANU:ACW:CHIS` `MANU:ACW:CLOS` | Ngưỡng dòng tham chiếu / trên / dưới |
| `MANU:STEP 001; :MANU:STEP?` | Chọn step |
| `TEST:RET ON; :TEST:RET?` | Cho phép máy trả kết quả về PC |
| `FUNC:TEST ON; :FUNC:TEST?` | **Bắt đầu phép thử** |
| `FUNC:TEST OFF :FUNC:TEST?` | **Dừng / cắt phát áp** |
| `MEAS?` | Đọc kết quả đo thật, ví dụ `OK,0.506kV,000.0mA,001.0s,PASS` |

Parser `MEAS?` **dựa trên đơn vị** (`kV`/`mA`/`s`), không theo vị trí cột, nên chịu được
các khác biệt định dạng giữa các firmware máy; có fallback về cách cắt chuỗi cổ điển.

### 2.2 Bảng điều khiển Arduino — `ArduinoLink.cs`

Cổng `ComPortControl` (mặc định `AUTO` = tự dò cổng trả lời `OK PONG`), 115200 8N1.
Xem `arduino\README.md` để biết **bảng chân**, cách nạp firmware và bảng lệnh đầy đủ.

| Board → PC | PC → Board |
|---|---|
| `*TESTON#` (bấm nút START) | `*R` (reset) |
| `*CANCEL#` (ABORT / nắp mở / mất unit / watchdog) | `*PASS#` / `*FAIL#` (đèn + còi) |
| `OK ...` / `ERR ...` / `EVT ...` | `PING` `STATUS?` `INTERLOCK?` `UNIT?` `MEAS?` `TEST START` `TEST ABORT` `RESET` |

Firmware **chủ động ngắt relay** khi interlock mở / mất unit / quá 30 s — không bypass được từ PC.

---

## 3. Báo cáo, pass rate và chương trình phép thử

Cấu trúc thư mục cạnh file `.exe` (tức là trong `bin\Debug\net8.0-windows\` khi chạy
từ Visual Studio / `dotnet run`). Giống hệt bản `Hipot_GPT9801` cũ, mở lại được dữ liệu cũ:

```
Config\
  SystemSetting.xml   cổng serial HIPOT + Control Board (XML serialize, có comment cũng đọc được)
  Program.ini         tên file chương trình đang chọn (vd program.csv)
  pw.org              mật khẩu admin (mặc định "admin")
program\
  program.csv         tham số phép thử
Report\<ten CT>\
  17-10-2026_NgayReport.csv   báo cáo ca ngày
  17-10-2026_DemReport.csv    báo cáo ca đêm
passrate\<ten CT>\
  17-10-2026_Ngay.csv         thống kê Total/Pass/Fail của ca
Log\
  2026-10-17_Log.csv          LOG VẬN HÀNH - mọi hoạt động (kết nối, lệnh serial, kết quả)
```

### Log vận hành (`Log\`)

Ghi các hoạt động vận hành cần thiết ra CSV theo ngày (tự xoay vòng khi sang ngày mới), lọc heartbeat `EVT HB` ở cả màn hình và tệp:

```
"Thoi_gian","Muc_do","Noi_dung"
"24/10/2026 | 17:53:41.113 (UTC+07:00)","STEP","[Bước 2] Đang kiểm tra kết nối GPT-9801 và bảng điều khiển..."
"24/10/2026 | 17:53:41.160 (UTC+07:00)","ERROR","[HIPOT] [LỖI] Không tìm thấy cổng COM2."
```

- Cột `Muc_do` tự suy ra từ nội dung: `INFO` / `STEP` / `PROGRAM` / `ERROR` / `STOP` / `WARN`
- Thời gian lấy một lần khi sự kiện được ghi nhận, dùng chung cho tệp CSV và màn hình. Tên tệp theo đúng ngày của sự kiện, kể cả khi qua nửa đêm.
- Cột thời gian mới là văn bản `dd/MM/yyyy | HH:mm:ss.fff (UTC±HH:mm)`, giữ đủ ngày/giờ/mili giây và múi giờ máy chạy khi mở bằng Excel. Kéo rộng cột A để đọc đủ nội dung. Tệp cũ đã được Excel nhận dạng thành ngày/giờ có thể đặt định dạng cột A là `dd/mm/yyyy hh:mm:ss.000`.
- Bỏ heartbeat bình thường `EVT HB` (kể cả kèm thời gian hoạt động); giữ kết quả PASS/FAIL, thay đổi trạng thái, kết nối, dừng khẩn, lỗi và cảnh báo. Heartbeat có mã lỗi/cảnh báo sự cố vẫn được ghi lại.
- **UTF-8 có BOM** (`EF BB BF`) → Excel mở file `.csv` tự nhận diện UTF-8 và đọc tiếng Việt đúng.
  Đây là điểm cần lưu ý: nếu **không** có BOM thì Excel trên Windows sẽ mở file bằng mã ANSI
  (CP1258 trên máy Việt) và đọc `Bước` thành chuỗi rác `BÆ°µc`. File log cũ ghi thiếu BOM sẽ
  được phần mềm **tự chép lại có BOM** ngay khi mở ra, không mất nội dung.
- File `Report\*.csv` và `passrate\*.csv` **giữ nguyên không BOM** (nội dung toàn ASCII) để
  phần mềm cũ `Hipot_GPT9801.exe` vẫn đọc được.
- Ghi được từ mọi thread; ghi file trước rồi mới hiển thị nên **không mất log** nếu app bị tắt bất ngờ
- Nếu file đang bị Excel giữ khóa thì phần mềm **tự ngừng ghi** và cảnh báo một lần trên màn hình;
  đóng Excel rồi bấm nút **Mở thư mục nhật ký** là ghi tiếp
- Nút **Mở thư mục nhật ký** (tab Trang chính, góc phải) mở thẳng thư mục `Log\` trong Explorer

Kiểm tra ghi nhật ký, định dạng thời gian và bộ lọc heartbeat: `dotnet run --project tests\LogWriterTests\LogWriterTests.csproj`.

Ca ngày/đêm chỉnh trong `config.ini` → `[Shift] DayStartHour=8`, `NightStartHour=20`.

**Định dạng `program.csv`** (16 dòng, nhãn xen kẽ giá trị — đọc 0-based):

| Dòng | Nội dung |
|---|---|
| 0 / 1 | `Model name` / tên chương trình |
| 2 / 3 | `Voltage test (kV)` / điện áp |
| 4 / 5 | `Frequency test (Hz)` / tần số |
| 6 / 7 | `Time test (S)` / thời gian thử |
| 8 / 9 | `Ramup test (S)` / thời gian ramp |
| 10 / 11 | `Reference test (mA)` / dòng tham chiếu |
| 12 / 13 | `High set (mA)` / ngưỡng trên |
| 14 / 15 | `Low set (mA)` / ngưỡng dưới |

**Dòng báo cáo** (giống bản cũ, kể cả dấu phẩy cuối):

```
No,Name,V test(kV),Freq (Hz),Time test (S),Ramup test (S),Ref (mA),Hi set (mA),Low set (mA),Voltage result (kV),Current result (mA),Time measure (S),Result
455,program,0.5,50,1,0.5,0,20,0,0.506,000.0,001.0,PASS,
```

---

## 4. Giao diện

- **Tab Home**: lưới 12 cột (thông số chương trình + kết quả đo), thông tin ứng dụng,
  Model/Program, trạng thái **HIPOT** và **CONTROL BOARD**, đồng hồ, PASS/FAIL/FPY/TOTAL,
  nút **Chọn CT** / **Sửa CT** / **Đăng nhập**, nút **Kết nối lại** và **Mở thư mục log**,
  log serial, ô TEST RESULT, checkbox fixture, nút **START TEST** và **STOP**.
- **2 biểu đồ kết quả** ở hàng cuối tab Home (giống màn hình TEST của `Hipot_GPT9801`):
  - **Biểu đồ cột** `TOTAL` (xanh dương) / `PASS` (xanh lá) / `FAIL` (đỏ), nhãn số lượng
    trên đầu mỗi cột - tương đương `chart1` + series `PassRate1` của bản cũ.
  - **Biểu đồ tròn** `PASS` / `FAIL` kèm % trên từng miếng, FPY và tổng số phép thử -
    tương đương `chart2` + series `PassRate2` của bản cũ.
  - Số liệu đọc từ `passrate\{ten CT}\{dd-MM-yyyy}_{Ngay|Dem}.csv` của **ca hiện tại**,
    nạp lại khi mở app / đổi chương trình và cập nhật ngay sau mỗi phép thử.
  - Vẽ bằng GDI+ (`ResultCharts.cs`), không cần thư viện biểu đồ ngoài.
- **Tab Cài đặt**: cấu hình cổng serial của cả 2 thiết bị, chia làm 2 nhóm rõ ràng:

  | Nhóm | Các ô |
  |---|---|
  | **MÁY HIPOT - GW Instek GPT-9801 (RS-232)** | Cổng COM · Baud rate · Parity · Data bits · Stop bits + nút **Quét** |
  | **BẢNG ĐIỀU KHIỂN - Arduino Control Board** | Cổng COM · Baud rate · Parity · Data bits · Stop bits + nút **Quét** |

  Bên dưới là nhóm **Thao tác** với nút **Lưu cấu hình** (ghi ngay ra
  `Config\SystemSetting.xml` trong thư mục bin), **Áp dụng &amp; Kết nối lại**
  (lưu rồi dò/ghép nối lại cả hai thiết bị ngay) và **Mở thư mục cấu hình**,
  kèm dòng hiển thị đường dẫn file cấu hình đang dùng.

  Nhóm **Bảo mật**: nhập `123456` rồi bấm **Mở khóa** để sửa cài đặt;
  `root123456` còn hiện phần `office.dat`. Lưu thành công sẽ khóa lại và ẩn phần này.

  Sau khi lưu, chỉ cần **khởi động lại phần mềm** là giá trị được đọc từ
  `Config\SystemSetting.xml`; còn **START** dùng luôn các cổng đã ghép nối
  sẵn lúc mở app — không đọc lại file giữa chừng.
- **Đăng nhập** (`Đăng nhập`): mật khẩu lưu ở `Config\pw.org` (mặc định `admin`),
  sai quá 3 lần thì đóng, có chức năng đổi mật khẩu. Mới vào được **Sửa CT**.
- **Chọn CT** / **Sửa CT**: sửa tham số phép thử rồi tự nạp lại lên GPT-9801.

---

## 5. Chạy chương trình

1. Cài .NET 8 SDK trên Windows.
2. `dotnet build Electronic_Safely\GPT9000_WinForms_TestSystem.csproj`
   (hoặc mở `.csproj` bằng Visual Studio 2022).
3. Chạy file `.exe` ở `bin\Debug\net8.0-windows\` — thư mục này đã có sẵn
   `config.ini`, `Config\`, `program\`.
4. Nạp firmware Arduino theo `arduino\README.md` (hoặc `arduino\nap-firmware.bat`).
5. Sửa `Config\SystemSetting.xml` cho đúng cổng COM của GPT-9801.

Kiểm tra logic thuần (parser, đọc/ghi file, định dạng báo cáo) không cần phần cứng:

```
dotnet run --project tools\apptest\apptest.csproj
```

Kiểm tra bảng điều khiển qua cổng serial:

```
dotnet run --project tools\cbtest\cbtest.csproj -- -auto        # dò cổng, PING/STATUS/MEAS
dotnet run --project tools\cbtest\cbtest.csproj -- -p COM6 -cmd "MEAS?"
```

---

## 6. Thông tin mặc định

| Hạng mục | Giá trị |
|---|---|
| Mật khẩu cài đặt / root | `123456` / `root123456` |
| Admin password (`Config\pw.org`) | `admin` |
| HIPOT | `COM2` @ 115200 8N1 (sửa trong tab **Cài đặt**) |
| Control Board | `AUTO` @ 115200 8N1 (tự dò; sẽ hiện đúng cổng đã ghim) |

> **Tự ghim cổng Board**: `ComPortControl=AUTO` sẽ quét toàn bộ cổng serial. Trên máy có
> cổng Bluetooth (COM BthModem) việc quét có thể mất **hơn 15 giây**, vì mỗi
> `SerialPort.Open()` trên cổng Bluetooth tốn vài giây. Vì vậy sau lần dò được đầu tiên,
> phần mềm **tự ghi cổng đó vào `Config\SystemSetting.xml`** → các lần mở sau chỉ mất
> khoảng **1 giây**. Giá trị đã ghim sẽ hiện ngay trong ô **Cổng COM** của tab **Cài đặt**,
> nên bạn luôn thấy rõ máy đang nối vào cổng nào. Nếu cổng đã ghim bị gỡ ra thì phần mềm
> tự quét lại một lần rồi ghim lại. Muốn quét lại từ đầu, gõ lại `AUTO` vào ô đó rồi bấm
> **Lưu cấu hình**.
| Baud baudrate firmware | `115200` (`#define SERIAL_BAUD` trong `.ino`) |

---

## 7. Trước khi dùng cho thiết bị thật

- Lấy **GPT-9000/9000A User Manual / Remote Control Command Reference** đúng model để
  đối chiếu cú pháp `MANU:*` (từng firmware máy có thể khác chút về tên tham số).
- Kiểm tra class `HipotLink.SetProgram()` — nếu máy báo `ERR` với lệnh nào, sửa đúng
  tên lệnh theo manual rồi chạy lại.
- Firmware HV chỉ là mức **DEMO**: nó điều khiển relay, không đo/thiết kế an toàn cao áp.
