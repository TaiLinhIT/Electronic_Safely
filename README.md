# GPT-9000 WinForms Test System

## Nội dung
- Tab **Home**: tên chương trình, nhãn logo HAN (placeholder), DataGridView item/value/low limit/up limit/result, thông tin app, cycle time, PASS/FAIL/FPY/TOTAL, log, trạng thái PASS/FAIL/IDLE.
- Tab **Setting**: fixture, database server/name/user/password/table, model tester, interface, COM port và baud rate.
- Cấu hình đọc/ghi tại `config.ini`.
- Trường mật khẩu database được che. Muốn hiện mật khẩu phải nhập `SettingsPassword` trong `config.ini`.
- Nút START TEST hiện chạy **DEMO/SIMULATION**, không phát lệnh đến GPT-9000 và không thực hiện phép thử điện áp cao.

## Chạy chương trình
1. Cài .NET 8 SDK trên Windows.
2. Mở `GPT9000_WinForms_TestSystem.csproj` bằng Visual Studio 2022.
3. Chọn target `Any CPU` hoặc `x64`, Build rồi Run.
4. Sửa `config.ini` nếu cần. Khi chạy, ứng dụng đọc file `config.ini` ở thư mục output (thường là `bin\\Debug\\net8.0-windows\\`).

## Thông tin mặc định
- Settings password: `admin123` (đổi ngay trước khi sử dụng thực tế).
- Database/server chỉ được lưu cấu hình; project mẫu chưa kết nối DB.
- Tester mặc định: GPT-9000 / RS232 / COM1 / 9600 baud.

## Trước khi kết nối thiết bị thật
Quick Start Guide xác nhận RS232/USB là các giao diện trong mục Common Utility, nhưng không cung cấp đủ cú pháp lệnh remote, định dạng trả lời hoặc mapping kết quả đo. Cần lấy **GPT-9000/9000A User Manual / Remote Control Command Reference** đúng model và xác nhận cấu hình giao tiếp. Sau đó triển khai driver riêng, timeout/retry, parser kết quả và trạng thái an toàn.
Không dùng demo này để điều khiển thiết bị cao áp hoặc đánh giá an toàn sản phẩm.
# Electronic_Safely
