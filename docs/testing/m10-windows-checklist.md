# M10 — Windows 11 manual checks

Chạy bản build M10 sau khi tự thoát phiên EasyToDoList đang dùng. Không chạy hai phiên bản khác nhau trên cùng database để kiểm thử.

- [ ] Mở Dashboard, chuyển Overlay, đóng bằng X: tiến trình còn sống và tray có Overlay / Dashboard / Exit. Cả ba mục hoạt động; double-click tray mở Dashboard.
- [ ] Mở executable lần hai khi cửa sổ đang ẩn và khi minimize: phiên đầu được focus, phiên thứ hai thoát.
- [ ] Chạy Focus, ẩn vào tray và để hết thời gian: thưởng đúng một lần, có thông báo; mở lại thấy trạng thái Awaiting. Tắt riêng từng loại notification và kiểm tra lại.
- [ ] Tạo nhiệm vụ có deadline hôm nay: nhận thông báo đến hạn. Để qua local midnight: Today đổi trạng thái, có phạt và thông báo quá hạn đúng một lần. Resume sau nhiều ngày cũng reconciliation đúng.
- [ ] Chạy Focus, ghi thời gian còn lại, Sleep rồi Resume: timer Paused, không trừ thời gian ngủ hoặc tự thưởng; UI vẫn phản hồi. Kiểm tra cả Dashboard và tray.
- [ ] Bật/tắt auto-start, lần lượt dùng Lưu cài đặt và Exit. Kiểm tra giá trị `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\EasyToDoList`, đường dẫn executable có dấu ngoặc kép và `--tray`. Đăng nhập lại để xác nhận mở ở tray.
- [ ] Exit khi Focus đang chạy: tiến trình biến mất, không còn thông báo nền; mở lại thấy Paused. Thử Windows shutdown/logoff: app không phủ quyết yêu cầu của Windows.

Thông báo dùng NotifyIcon của Windows; Do Not Disturb và cài đặt notification hệ thống có thể ẩn chúng. Log tại `%LocalAppData%\EasyToDoList\Logs`: hai file tối đa khoảng 256 KiB/file, không có title To-do hoặc exception message.

Giới hạn: forced shutdown/crash có thể phục hồi từ checkpoint trước đó tối đa khoảng 30 giây. Chưa có background agent khi process đã thoát.
