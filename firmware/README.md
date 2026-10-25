# Cài thời gian chu trình từ phần mềm

Firmware `GPT9800_ControlBoard/GPT9800_ControlBoard.ino`, phiên bản **1.4.0**, bổ sung lệnh cài thời gian. Nạp firmware một lần vào Arduino Uno/Nano ATmega328P rồi sử dụng bản EXE mới. Việc build/tự kiểm tra không nạp board và không chạy thiết bị thật.

## Sử dụng

1. Mở tab **Cài đặt**, nhập mật khẩu cài đặt để mở khóa.
2. Chỉnh các ô thời gian theo **giây**; ví dụ `2,500` là 2,5 giây khi Windows dùng dấu phẩy thập phân.
3. Nhấn **Lưu cấu hình**. Khi đang kết nối, bảng điều khiển phải xác nhận đủ các giá trị; dòng trạng thái sẽ báo đã áp dụng. Nếu chưa kết nối, phần mềm lưu và gửi khi kết nối lại.
4. Khi đổi cổng COM, dùng **Lưu và kết nối lại** để lưu và kết nối thiết bị ở cổng mới.

Thông số lưu trong `Config/SystemSetting.xml` cạnh EXE, ở phần tử `Timing`, đơn vị mili giây. Mở lại phần mềm sẽ đọc các giá trị này. File cấu hình cũ chưa có `Timing` vẫn đọc được và dùng thời gian mặc định; cổng COM cũ được giữ nguyên. Chỉnh sửa bị chặn khi đang đóng, thử, rút hoặc mở đồ gá.

| Ô cài đặt | Mặc định | Phạm vi (giây) |
|---|---:|---:|
| Đóng đồ gá trước khi đẩy kim | 2,5 | 0,1–7,5 |
| Đẩy kim trước khi cho phép đo | 1,2 | 0,1–7,5 |
| Rút kim trước khi mở đồ gá | 1,2 | 0,1–7,5 |
| Mở đồ gá trước khi sẵn sàng | 2,5 | 0,1–7,5 |
| Chờ máy thử xác nhận sẵn sàng | 30 | 1–30 |
| Chờ thêm sau thử và tăng áp | 1,5 | 0–10 |
| Dự phòng chờ máy thử hoàn tất | 5 | 1–60 |
| Chờ phản hồi kết quả đo | 4 | 0,5–30 |
| Tạm giữ thông báo lỗi khởi động | 1,2 | 0–10 |
| Chờ sau khi kết thúc phép thử | 0,3 | 0–10 |
| Chờ tối đa đồ gá về vị trí nghỉ | 8 | 1–30 |

Chờ đồ gá về phải lớn hơn tổng thời gian rút kim và mở đồ gá ít nhất 0,5 giây. Dự phòng hoàn tất phải lớn hơn thời gian chờ thêm sau thử/tăng áp ít nhất 0,5 giây. Phần mềm báo rõ nếu các ô không tương thích và giữ cấu hình đã lưu trước đó.

## Nạp và trạng thái tương thích

- Mở file `.ino` ở trên trong Arduino IDE, chọn đúng board/cổng và nạp như firmware hiện tại. Hoặc dùng công cụ nạp sẵn có tại `../arduino/` ở thư mục dự án cha; file nguồn tại đó cũng được đồng bộ với bản này.
- File HEX đã build nằm ở `build/GPT9800_ControlBoard.hex`. Có thể build lại bằng `powershell -File tools/Build-ControlBoard.ps1` từ thư mục dự án WinForms.
- Bản 1.3.4 trở về trước chỉ chạy được với các thời gian cơ khí mặc định. Khi cài giá trị tùy chỉnh, phần mềm báo cần nâng cấp, không coi đó là đã áp dụng.
- Các thời gian trên board lưu trong RAM; không ghi EEPROM. Mỗi lần kết nối, phần mềm gửi lại giá trị từ XML. Nếu board khởi động lại trong khi mở phần mềm, xác nhận thời gian cũ bị hủy; nhấn **Kết nối lại** hoặc lưu lại khi board đã nghỉ.
- Nếu `USE_POS_SENSOR=1`, hai bước đóng đồ gá/đẩy kim vẫn chờ công tắc hành trình theo thiết kế hiện có. Thời gian cấu hình điều khiển hai bước này khi `USE_POS_SENSOR=0` (mặc định).

## Giao thức cho người phát triển

```text
TIMING?
OK TIMING V=1 CLOSE=2500 OUT=1200 BACK=1200 OPEN=2500 ARM=30000

TIMING SET 3500 600 900 1800 2000
OK TIMING V=1 CLOSE=3500 OUT=600 BACK=900 OPEN=1800 ARM=2000
```

Đơn vị là mili giây; thứ tự: đóng đồ gá, đẩy kim, rút kim, mở đồ gá, chờ PC xác nhận. Cập nhật cả bộ sau khi kiểm tra đủ dữ liệu. Chỉ nhận khi `IDLE`, relay/liên động/van đều tắt. Khi đang hoạt động trả `ERR TIMING_BUSY`; sai số lượng tham số trả `ERR TIMING_ARGS`, sai giá trị trả `ERR TIMING_RANGE`. Không có giá trị nào được đổi khi lệnh bị từ chối.

Giữ thứ tự đóng đồ gá → đẩy kim → cho phép đo → PC xác nhận → relay; khi trả kết quả hoặc dừng: cắt relay/liên động → rút kim → mở đồ gá. Lệnh cấu hình không nới watchdog hành trình 8 giây hoặc watchdog relay 30 giây. Nút bắt đầu vật lý và lệnh từ PC dùng cùng thông số trên board.

## Kiểm tra không nối thiết bị

```powershell
powershell -File tests/FirmwareTimingTests/run-sim.ps1
dotnet run --project tests/CycleTimingTests/CycleTimingTests.csproj -- tests/FirmwareTimingTests/build/sim.exe
dotnet run --project tests/LicenseTests/LicenseTests.csproj
dotnet run --project tests/ResultDisplayTests/ResultDisplayTests.csproj
```

Mô phỏng nhúng đúng file `.ino` để kiểm tra GPIO và mốc chuyển bước; bộ kiểm tra C# trao đổi trực tiếp với firmware mô phỏng, kiểm tra lưu/đọc XML, khóa cài đặt và bố cục. Các kiểm tra này không xác nhận thời gian hành trình điện/khí thật.
