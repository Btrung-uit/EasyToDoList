# EasyToDoList — Hội quản nhiệm vụ

Ứng dụng Windows kết hợp danh sách công việc, Pomodoro và tiến trình nhân vật RPG pixel art. Dữ liệu lưu cục bộ; không cần tài khoản hoặc Internet khi sử dụng.

## Cài đặt và mở ứng dụng

- Hỗ trợ Windows 11 x64. Bản `0.1.0-rc.1` là ứng viên kiểm thử, chưa phải bản phát hành ổn định.
- Chạy `EasyToDoList-<version>-win-x64-setup.exe`. Installer cài cho người dùng hiện tại, không yêu cầu quyền quản trị hoặc cài .NET trước.
- Mở **EasyToDoList** từ Start Menu; shortcut Desktop là tùy chọn.
- Bản portable: giải nén toàn bộ ZIP vào một thư mục có quyền ghi rồi chạy `EasyToDoList.App.exe`. Không chỉ chép riêng file EXE.
- Chỉ chạy một instance cho mỗi tài khoản Windows. Mở lần hai sẽ đưa instance đang có ra trước.

## Dùng hằng ngày

- **Hôm nay:** nhập tên rồi Enter hoặc **+ Thêm nhiệm vụ**. Mở phần ngày để chọn ngày bắt đầu/hạn chót; mặc định là hôm nay. **Sửa** cập nhật nhiệm vụ, **Hủy** giữ record trong lịch sử.
- Checkbox hoàn thành áp dụng phần thưởng hiện có. **Hoàn tác** trên thông báo có hiệu lực 5 giây. Tiến độ hôm nay gồm nhiệm vụ còn mở và việc hoàn thành hôm nay; nhiệm vụ đã hủy không tính vào tổng.
- **Lịch sử:** xem công việc theo tuần.
- **Pomodoro:** chọn preset/cấu hình hợp lệ, bắt đầu, tạm dừng hoặc tiếp tục. Hết phiên chờ bạn bắt đầu phiên tiếp theo. **Mở cửa sổ tập trung** chuyển sang Overlay mà không tạo timer khác.
- **Cài đặt:** âm lượng, từng âm báo, animation, Reduced Motion, thông báo và khởi động cùng Windows.
- Đóng bằng **X** chỉ ẩn vào tray. Double-click tray mở Dashboard; **tray → Exit** mới thoát hẳn. App phải còn chạy để đếm giờ và gửi thông báo.
- Sau Sleep hoặc mở lại sau shutdown/crash, phiên được khôi phục Paused; thời gian máy tắt không tính là tập trung. Forced kill/mất điện có thể khôi phục từ checkpoint cũ khoảng 30 giây.

## Dữ liệu, nâng cấp và gỡ cài đặt

- Chương trình được cài ở `%LocalAppData%\Programs\EasyToDoList`.
- Database: `%LocalAppData%\EasyToDoList\easytodolist.db`.
- Settings, nhiệm vụ, Pomodoro và reward ledger nằm trong database. Log giới hạn dung lượng ở `%LocalAppData%\EasyToDoList\Logs`; log không ghi tên nhiệm vụ.
- Trước nâng cấp: thoát qua tray → Exit và sao lưu toàn bộ thư mục dữ liệu trên vào nơi an toàn. Đừng chỉ bấm X. Chạy installer mới vào cùng thư mục cũ; lần mở đầu tự chạy migration hiện có.
- Không mở database bằng phiên bản cũ hơn sau khi nâng cấp schema. Khi cần khôi phục: thoát app, giữ một bản sao dữ liệu đang có, rồi khôi phục bản backup phù hợp với phiên bản app.
- Gỡ qua Windows **Installed apps → EasyToDoList → Uninstall**. Installer xóa executable/shortcuts và startup entry thuộc đúng bản cài này; **không xóa database, settings, lịch sử hoặc logs**. Cài lại sẽ dùng tiếp dữ liệu cũ.
- Chạy từ source hoặc portable cũng dùng thư mục dữ liệu trên; chúng không phải profile riêng. Không mở nhiều phiên bản để thử trên dữ liệu cá nhân.

## Kiểm tra file tải về và xử lý lỗi

Đối chiếu SHA256 với `SHA256SUMS.txt` do cùng lượt build tạo ra:

```powershell
Get-FileHash .\EasyToDoList-0.1.0-rc.1-win-x64-setup.exe -Algorithm SHA256
```

Bản ứng viên chưa có chữ ký số; Windows có thể hiện cảnh báo nhà phát hành. Kiểm tra nguồn tải và hash trước khi quyết định chạy. Nếu app đang chạy, installer yêu cầu thoát bằng tray → Exit, không ép đóng phiên Pomodoro.

Nếu app không mở: kiểm tra tray trước, rồi đọc log và thông báo đường dẫn database. Khi migration lỗi, giữ nguyên database/backup để chẩn đoán; không xóa dữ liệu để thử lại. Do Not Disturb hoặc cài đặt Windows có thể ẩn notification.

## Phát triển và tạo bộ cài

Cần Windows, .NET SDK theo `global.json` và Inno Setup 6 (ISCC.exe). Source không chứa các bộ cài sinh ra.

```powershell
dotnet restore EasyToDoList.sln
dotnet run --project src/EasyToDoList.App
dotnet test EasyToDoList.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet build EasyToDoList.sln -c Release --no-restore -warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-release.ps1
```

Script tạo installer, portable ZIP và SHA256 manifest ở `artifacts/release/<version>`. Có thể truyền `-CompilerPath` và `-Version`; default version lấy từ project. Publish self-contained, giữ WPF và SQLite native, không trim/single-file. [Release checklist](https://github.com/Btrung-uit/EasyToDoList/blob/main/docs/testing/manual-release-checklist.md) phân biệt kiểm chứng đã chạy với các gate thủ công còn mở.

Nguồn asset và runtime license/notices được kèm trong `ASSET-LICENSES.md` và thư mục `runtime-notices` của bản phân phối.

[Asset provenance](https://github.com/Btrung-uit/EasyToDoList/blob/main/Resources/ASSET-LICENSES.md) · [Architecture](https://github.com/Btrung-uit/EasyToDoList/blob/main/docs/architecture/system-design.md) · [Milestones](https://github.com/Btrung-uit/EasyToDoList/blob/main/TASKS.md)
