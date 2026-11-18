# Dashboard Quest Journal — 18/11/2026

Phạm vi: cải tiến dashboard WPF đang có; giữ .NET 10, SQLite, assets và các service nghiệp vụ. Bản phác thảo của người dùng là tham chiếu bố cục, không phải nguồn dữ liệu production.

## Thay đổi UI/UX

- Header gọn, subtitle ngắn; ba cột cách nhau 16 DIP, lề ngoài 20 DIP. Sidebar 176 DIP thu còn 64 DIP khi cửa sổ dưới 1080 DIP; nhãn accessibility và tooltip vẫn đầy đủ.
- Navigation có icon Path nhẹ, nền riêng cho trang đang chọn; tái dùng hover, disabled và keyboard focus hiện có.
- Hôm nay có ngày `dd/MM/yyyy`, số hoàn thành/tổng và progress bar. Tổng gồm việc còn mở của Today (kể cả quá hạn) cộng việc hoàn thành hôm nay; không cộng việc đã hủy. Completion đang animation không bị đếm hai lần.
- Số đã hoàn thành lấy từ `ITodoHistoryQuery`, đúng khi mở lại app. Đọc nhiệm vụ, lịch sử và publish snapshot dùng chung một gate, tránh tải lại chen giữa completion.
- Ô tên rộng, placeholder, Enter gọi SaveCommand; lịch bắt đầu/hạn chót mở rộng khi chỉnh sửa hoặc lỗi ngày. DatePicker dùng vi-VN; validation/service hiện tại vẫn quyết định dữ liệu hợp lệ.
- Quest có checkbox, tên wrap, ngày hạn, trạng thái và Sửa/Hủy. Hủy cần xác nhận, vẫn là hủy mềm và giữ lịch sử. Hoàn thành giữ animation/toast và Hoàn tác 5 giây; sau đó công việc chuyển sang Lịch sử theo logic cũ.
- Empty state tái dùng knight sprite, phân biệt chưa bắt đầu với đã hoàn thành hết; nút tạo đưa focus về ô tên. Danh sách cuộn theo pixel và empty state có vùng cuộn riêng khi thiếu chiều cao.
- Adventurer panel hiển thị hạng/cấp, EXP trong cấp/ngưỡng kế tiếp, EXP bar và coin từ dữ liệu thật. Không thay công thức hay preview phần thưởng bằng số giả.
- Pomodoro sidebar hiển thị countdown, phase/status, Start/Pause/Resume và “Mở cửa sổ tập trung”; tất cả dùng PomodoroOverlayViewModel cùng timer/service hiện tại.
- Toast thêm/sửa thành công tự hết sau 4 giây, dùng feedback tick hiện có. Giữ thông báo lỗi và live regions.

## File thay đổi

- `src/EasyToDoList.App/MainWindow.xaml`, `MainWindow.xaml.cs`: layout, navigation responsive, profile/Pomodoro, toast.
- `src/EasyToDoList.App/Themes/Controls.xaml`: navigation, primary button và progress bar dùng token sẵn.
- `src/EasyToDoList.App/Features/Today/TodayView.xaml`, `TodayView.xaml.cs`: form, quest, empty state, xác nhận hủy và focus.
- `src/EasyToDoList.App/Features/Today/TodayViewModel.cs`, `TodoRowViewModel.cs`: dữ liệu progress, feedback và trạng thái hiển thị.
- `src/EasyToDoList.App/Features/Dashboard/DashboardViewModel.cs`: trang đang chọn.
- `src/EasyToDoList.App/App.xaml.cs`: nối query lịch sử sẵn có vào Today.
- `tests/EasyToDoList.App.Tests/Features/CompletionFeedbackTests.cs`: progress/Undo và race có điều khiển.
- `tests/EasyToDoList.App.Tests/Features/DashboardProgressTests.cs`: SQLite thật cho create/edit/cancel/complete/Undo, reload và đổi ngày.
- `tests/EasyToDoList.App.Tests/Features/CompletionWpfTests.cs`: acceptance mới, resize, empty state, nhập nhanh và toast; giữ regression Overlay/Pomodoro cũ.
- `DESIGN.md`, `TASKS.md`, tài liệu này: cập nhật mô tả và evidence.

## Bảo toàn và giới hạn

Không đổi Core, Infrastructure, schema/migration, file dữ liệu cá nhân, cơ chế EXP/coin, thời gian Undo hay lifecycle timer. Lịch sử, Cài đặt, tray và Overlay tiếp tục dùng cùng flow. Không thêm dependency, hệ thống kinh tế hoặc liên kết nhiệm vụ/Pomodoro giả.

Kiểm thử dùng SQLite in-memory và cửa sổ WPF riêng. Không đóng instance cá nhân đang chạy. Ảnh runtime nằm trong `tests/EasyToDoList.App.Tests/bin/Debug/net10.0-windows/dashboard-quest-journal*.png`; chúng có ngày/nhiệm vụ kiểm thử, không phải dữ liệu production. Kiểm thử mouse/alpha cần truy cập desktop ngoài sandbox. DPI 96/144/192 là raster mô phỏng; DPI màn hình thật, nhiều màn hình, Narrator và profiling dài hạn vẫn chưa được xác minh.

## Chạy lại

Snapshot cuối: **228/228 tests pass** (App 81, Core 99, Infrastructure 48), build warnings-as-errors **0 warning / 0 error**. Detector UI trả `[]`. Reviewer phát hiện race giữa reload/completion; regression tái hiện đỏ trước fix và xanh sau khi dùng gate chung. Hai lượt kiểm tra desktop `IsMouseOver` sau 75 ms thất bại, nhưng focused và full run cuối đều pass; test giữ diagnostic cursor/HWND để điều tra nếu tái diễn. Không sửa production timer/opacity để né lỗi kiểm thử.

```powershell
dotnet run --project src/EasyToDoList.App
```

Nếu app đã chạy, single-instance sẽ mở instance đang có; thoát qua tray → Exit rồi chạy lại để xem binary mới.

```powershell
dotnet test EasyToDoList.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet build EasyToDoList.sln --no-restore -warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
```
