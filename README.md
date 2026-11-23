# EasyToDoList — Hội quản nhiệm vụ

**Ứng dụng Windows kết hợp quản lý công việc, Pomodoro và tiến trình nhân vật RPG pixel art.** Bạn có thể đặt hạn chót, theo dõi việc đã hoàn thành và tích lũy EXP, Coin cho nhân vật. Ứng dụng có giao diện tiếng Việt, chạy offline và lưu dữ liệu trên máy.

[![CI](https://github.com/Btrung-uit/EasyToDoList/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Btrung-uit/EasyToDoList/actions/workflows/ci.yml)

**[Tải EasyToDoList cho Windows 11 x64](https://github.com/Btrung-uit/EasyToDoList/releases)** · [Báo lỗi hoặc góp ý](https://github.com/Btrung-uit/EasyToDoList/issues)

![Dashboard EasyToDoList hiển thị danh sách nhiệm vụ, tiến trình nhân vật và đồng hồ Pomodoro](docs/screenshots/dashboard.png)

*Dashboard của bản v0.1.0-rc.3 với dữ liệu minh họa từ kiểm thử giao diện.*

## Tính năng

- **Quản lý nhiệm vụ** với ngày bắt đầu, hạn chót, tiến độ trong ngày và lịch sử theo tuần. Có thể hoàn tác thao tác hoàn thành trong 5 giây.
- **Tập trung với Pomodoro** bằng cấu hình có sẵn hoặc thời lượng tùy chỉnh. Dashboard và cửa sổ Overlay dùng chung bộ đếm, nên có thể đổi giao diện giữa phiên.
- **Theo dõi tiến trình RPG** qua EXP, Coin, cấp độ và hạng của nhân vật hiệp sĩ pixel art.
- **Tích hợp Windows** với khay hệ thống, nhắc việc đến hạn hoặc quá hạn, thông báo khi hết phiên Pomodoro và tùy chọn khởi động cùng Windows. Có thể chỉnh âm lượng, tắt từng âm báo và bật chế độ giảm chuyển động.
- **Khôi phục phiên Pomodoro** ở trạng thái tạm dừng sau khi máy thức dậy hoặc khi mở lại ứng dụng sau khi tắt máy. Sau khi bị buộc thoát, bộ đếm có thể trở về mốc lưu gần nhất, cách thời điểm gián đoạn khoảng 30 giây.

## Tải và cài đặt

[**v0.1.0-rc.3**](https://github.com/Btrung-uit/EasyToDoList/releases/tag/v0.1.0-rc.3) là bản thử nghiệm cho **Windows 11 x64**. Cả hai gói đều kèm .NET runtime.

| Gói | Cách dùng |
| --- | --- |
| [Installer (.exe)](https://github.com/Btrung-uit/EasyToDoList/releases/download/v0.1.0-rc.3/EasyToDoList-0.1.0-rc.3-win-x64-setup.exe) | Chạy bộ cài rồi mở **EasyToDoList** từ Start Menu. Cài cho tài khoản hiện tại, không cần quyền quản trị. |
| [Portable (.zip)](https://github.com/Btrung-uit/EasyToDoList/releases/download/v0.1.0-rc.3/EasyToDoList-0.1.0-rc.3-win-x64-portable.zip) | Giải nén toàn bộ ZIP vào thư mục có quyền ghi rồi mở `EasyToDoList.App.exe`. |

Bộ cài thử nghiệm chưa có chữ ký số. Trước khi chạy, tính SHA-256 của tệp tải về bằng PowerShell. So sánh kết quả với [`SHA256SUMS.txt`](https://github.com/Btrung-uit/EasyToDoList/releases/download/v0.1.0-rc.3/SHA256SUMS.txt).

```powershell
Get-FileHash .\EasyToDoList-0.1.0-rc.3-win-x64-setup.exe -Algorithm SHA256
```

### Sử dụng lần đầu

1. Trong **Hôm nay**, nhập tên nhiệm vụ rồi nhấn **Enter**. Mở **Ngày bắt đầu và hạn chót** nếu cần lên lịch.
2. Trong **Pomodoro**, chọn thời lượng và bấm **Bắt đầu**. Dùng **Mở cửa sổ tập trung** để chuyển sang Overlay.
3. Bấm **X** để ẩn ứng dụng vào khay hệ thống trong khi bộ đếm tiếp tục chạy. Nhấp đúp biểu tượng khay để mở lại Dashboard, hoặc chọn **Exit** từ menu của biểu tượng để thoát hẳn.

## Dữ liệu và nâng cấp

| Nội dung | Vị trí mặc định |
| --- | --- |
| Thư mục cài đặt | `%LOCALAPPDATA%\Programs\EasyToDoList` |
| SQLite (nhiệm vụ, cài đặt, Pomodoro và phần thưởng) | `%LOCALAPPDATA%\EasyToDoList\easytodolist.db` |
| Nhật ký ứng dụng | `%LOCALAPPDATA%\EasyToDoList\Logs` |

- **Trước khi nâng cấp**, thoát qua biểu tượng khay → **Exit**, sao lưu toàn bộ thư mục `%LOCALAPPDATA%\EasyToDoList`, rồi chạy bộ cài mới vào vị trí cũ. Ứng dụng tự cập nhật schema khi mở lần đầu nếu cần.
- **Khi khôi phục bản cũ**, giữ một bản sao dữ liệu hiện tại rồi dùng bản sao lưu tương ứng với phiên bản ứng dụng cũ.
- Bản cài, portable và ứng dụng chạy từ mã nguồn dùng chung thư mục dữ liệu của tài khoản Windows. Khi gỡ qua **Installed apps**, dữ liệu và nhật ký vẫn được giữ lại để dùng khi cài lại.
- Nếu ứng dụng không xuất hiện, kiểm tra biểu tượng khay và nhật ký. Nếu xảy ra lỗi cập nhật cơ sở dữ liệu, giữ tệp gốc và bản sao lưu để chẩn đoán. Chế độ **Do Not Disturb** của Windows có thể ẩn thông báo.

## Phát triển

Cần Windows 11 x64 và [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) theo phiên bản trong [`global.json`](global.json). Các lệnh sau dùng tag của bản thử nghiệm để dựng đúng phiên bản được mô tả ở trên.

```powershell
git clone --branch v0.1.0-rc.3 https://github.com/Btrung-uit/EasyToDoList.git
cd EasyToDoList
dotnet restore EasyToDoList.sln
dotnet run --project src/EasyToDoList.App --no-restore
```

### Kiến trúc và kiểm thử

| Project | Trách nhiệm |
| --- | --- |
| [`EasyToDoList.App`](src/EasyToDoList.App) | Giao diện WPF theo MVVM, Dashboard, Overlay và tích hợp vòng đời cửa sổ. |
| [`EasyToDoList.Core`](src/EasyToDoList.Core) | Luật nhiệm vụ, mô hình trạng thái Pomodoro, EXP, Coin và thăng cấp. |
| [`EasyToDoList.Infrastructure`](src/EasyToDoList.Infrastructure) | Lưu trữ SQLite qua EF Core, migration và transaction. |

Khi hoàn thành hoặc hoàn tác một nhiệm vụ, ứng dụng cập nhật trạng thái và sổ giao dịch phần thưởng trong cùng một transaction. Bộ kiểm thử xUnit gồm kiểm thử nghiệp vụ, SQLite và giao diện WPF. [Thiết kế hệ thống](docs/architecture/system-design.md)

```powershell
dotnet test EasyToDoList.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet build EasyToDoList.sln -c Release --no-restore -warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
```

### Đóng gói

Cài **Inno Setup 6** rồi chạy lệnh sau từ thư mục gốc. Có thể truyền `-CompilerPath` nếu script chưa tìm thấy `ISCC.exe`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-release.ps1
```

Installer, ZIP portable và tệp SHA-256 được tạo trong `artifacts/release/<version>`. [Checklist phát hành](https://github.com/Btrung-uit/EasyToDoList/blob/v0.1.0-rc.3/docs/testing/manual-release-checklist.md) · [Milestones](TASKS.md)

## Giấy phép và nguồn tài nguyên

Repository hiện chưa khai báo giấy phép cho toàn bộ mã nguồn. Thông tin giấy phép và nguồn gốc của từng tài nguyên được ghi trong [`Resources/ASSET-LICENSES.md`](Resources/ASSET-LICENSES.md). Gói phân phối có kèm `ASSET-LICENSES.md`, `third-party-notices` và `runtime-notices`.
