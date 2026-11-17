# M10 — Windows 11 manual checks

Chạy bản Release hiện tại sau khi tự thoát phiên EasyToDoList đang dùng. Không chạy hai phiên bản khác nhau trên cùng database để kiểm thử.

- [x] Mở Dashboard, chuyển Overlay, đóng bằng X: tiến trình còn sống và tray có Overlay / Dashboard / Exit. Cả ba mục hoạt động; double-click tray mở Dashboard.
- [x] Mở executable lần hai khi cửa sổ đang ẩn và khi minimize: phiên đầu được focus, phiên thứ hai thoát.
- [ ] Chạy Focus, ẩn vào tray và để hết thời gian: thưởng đúng một lần, có thông báo; mở lại thấy trạng thái Awaiting. Tắt riêng từng loại notification và kiểm tra lại.
- [x] Tạo nhiệm vụ có deadline hôm nay: Windows hiển thị thông báo đến hạn, xác nhận bằng ảnh chụp thực tế.
- [ ] Để qua local midnight: Today đổi trạng thái, có phạt và thông báo quá hạn đúng một lần. Resume sau nhiều ngày cũng reconciliation đúng.
- [ ] Chạy Focus, ghi thời gian còn lại, Sleep rồi Resume: timer Paused, không trừ thời gian ngủ hoặc tự thưởng; UI vẫn phản hồi. Kiểm tra cả Dashboard và tray.
- [x] Bật/tắt auto-start qua Lưu cài đặt và Exit. Giá trị `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\EasyToDoList` có đường dẫn executable trong dấu ngoặc kép và `--tray`; tắt sẽ xóa giá trị. Chạy executable với `--tray` và mở lại bằng tray thành công.
- [ ] Đăng xuất/đăng nhập thật để xác nhận Windows tự mở app ở tray — người dùng chọn để kiểm thử sau ngày 17/11/2026.
- [x] Exit khi Focus đang chạy: tiến trình biến mất; mở lại thấy Paused và giữ nguyên thời gian còn lại. Exit từ tray trả exit code 0; Exit từ Dashboard cũng kết thúc process.
- [ ] Windows shutdown/logoff thật: app không phủ quyết yêu cầu của Windows — để kiểm thử sau cùng bước đăng xuất.

## Evidence ngày 17/11/2026

Executable Release từ source `1f4ad82`, Windows 11 Pro `10.0.26100`:

- **14/14 smoke assertions pass** trên executable thật: hai chế độ hiển thị,
  X-to-tray, single-instance từ hidden/minimized, foreground window, các mục tray,
  double-click, auto-start qua Save/Exit, `--tray`, Exit và khôi phục Focus Paused.
- **19/19 focused tests pass** cho Bootstrap và Shell: SQLite thật với clock giả,
  reward/notification deduplication, midnight, resume sau nhiều ngày, callback power,
  startup settings, IPC và rolling log. Đây không phải Sleep/Resume hay midnight thật.
- Full solution **211/211 tests pass**; build với warnings-as-errors **0 warning,
  0 error** trên snapshot cuối. Lượt này không sửa product code.
- Đã tạo nhiệm vụ đến hạn bằng UI và xác nhận banner Windows thật có tiêu đề
  **Nhiệm vụ đến hạn**, nội dung **Bạn có 1 nhiệm vụ cần xem trong EasyToDoList.**
  UI Automation ban đầu bỏ sót banner; ảnh chụp trực tiếp góc màn hình xác nhận
  notification-display pass cho trường hợp đến hạn. Evidence cục bộ:
  `artifacts/m10-verification/due-notification-corner.png` (không đưa vào bản phát hành).
- Không tạo thưởng/phạt trong database cá nhân. Các phiên/nhiệm vụ kiểm thử được
  xác định riêng, kiểm tra không có reward liên quan rồi xóa đúng các record đó
  trong transaction. Registry và cài đặt auto-start được khôi phục; không còn
  process EasyToDoList sau kiểm tra. Log hiện tại không chứa title To-do.
- Sleep/Resume và đăng xuất/đăng nhập được hoãn theo lựa chọn của người dùng.

Chưa đóng M10: còn thông báo Windows thực tế cho hoàn thành/quá hạn,
chạy hết Focus trong tray, qua midnight thật, Sleep/Resume và đăng nhập/shutdown.

## Chạy lại smoke không ngắt phiên Windows

Script yêu cầu Windows 11 có tên tray tiếng Anh, Windows PowerShell 5.1,
Node.js hỗ trợ `node:sqlite` (đã chạy với `24.19.0`), database đã khởi tạo và
không có phiên Pomodoro đang Running/Paused/Awaiting. Thoát app trước khi chạy;
tránh thao tác chuột/bàn phím trong khoảng 20 giây khi script điều khiển UI.
Script tạo một Focus ngắn để kiểm tra Exit rồi xóa riêng phiên đó nếu chưa có
reward; không thay đổi đồng hồ hệ thống, không sleep hoặc đăng xuất.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File docs/testing/m10-windows-smoke.ps1
```

Script có assertion cho từng bước và dừng khi sai; khôi phục auto-start trong
cleanup. Nếu cleanup báo lỗi, giữ nguyên các dữ liệu khác và kiểm tra record
kiểm thử được nêu trong lỗi, không xóa hoặc thay toàn bộ database.

Thông báo dùng NotifyIcon của Windows; Do Not Disturb và cài đặt notification hệ thống có thể ẩn chúng. Log tại `%LocalAppData%\EasyToDoList\Logs`: hai file tối đa khoảng 256 KiB/file, không có title To-do hoặc exception message.

Giới hạn: forced shutdown/crash có thể phục hồi từ checkpoint trước đó tối đa khoảng 30 giây. Chưa có background agent khi process đã thoát.
