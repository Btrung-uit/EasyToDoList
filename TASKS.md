# EasyToDoList — Coding Milestones

Tài liệu này là bảng điều phối triển khai MVP. Khi bắt đầu một mốc, chỉ làm đúng phạm vi của mốc đó; không tự động chuyển sang mốc tiếp theo.

## Cách sử dụng

- Yêu cầu theo mã, ví dụ: **“Thực hiện M01”**.
- Mỗi mốc phải bắt đầu bằng việc đọc lại `TASKS.md` và các tài liệu được liên kết.
- Ưu tiên TDD: test thất bại → code tối thiểu → test pass.
- Mỗi mốc kết thúc bằng `dotnet build -warnaserror`, `dotnet test` và một commit riêng.
- Chỉ đánh dấu `[x]` khi toàn bộ điều kiện hoàn thành đã đạt.
- Nếu một mốc phát hiện thiết kế sai, dừng lại và cập nhật tài liệu trước khi sửa lan sang mốc khác.

## Tài liệu nguồn

- [Product specification](docs/superpowers/specs/2026-11-11-easy-to-do-list-design.md)
- [System design & architecture](docs/architecture/system-design.md)
- [Detailed implementation plan](docs/superpowers/plans/2026-11-11-easy-to-do-list-mvp.md)
- [Pixel-art asset contract](assets/pixel-art/mvp/README.md)

## Trạng thái

| Mốc | Nội dung | Trạng thái | Phụ thuộc |
|---|---|---|---|
| M00 | Đặc tả, architecture và asset prototype | Hoàn thành | — |
| M01 | Solution foundation và SQLite bootstrap | Hoàn thành | M00 |
| M02 | Economy, profile, level và rank | Hoàn thành | M01 |
| M03 | To-do domain, transaction và reward ledger | Hoàn thành | M02 |
| M04 | Daily reconciliation, quá hạn và lịch sử | Hoàn thành | M03 |
| M05 | Pomodoro state machine và persistence | Hoàn thành | M02, M03 |
| M06 | WPF shell và medieval design foundation | Chưa bắt đầu | M01, M05 |
| M07 | Dashboard, To-do UI, lịch sử và Settings | Chưa bắt đầu | M04, M06 |
| M08 | Compact Overlay và Pomodoro UI | Chưa bắt đầu | M05, M06 |
| M09 | Animation hoàn thành, combo, Undo và reward feedback | Chưa bắt đầu | M03, M07, M08 |
| M10 | Windows lifecycle, tray, notification và startup | Chưa bắt đầu | M04, M05, M06 |
| M11 | Accessibility, visual polish và asset integration | Chưa bắt đầu | M07–M10 |
| M12 | Installer, release verification và tài liệu sử dụng | Chưa bắt đầu | M11 |

---

## M00 — Thiết kế và asset prototype

- [x] Chốt phạm vi MVP.
- [x] Chốt WPF + SQLite và modular monolith.
- [x] Chốt Dashboard/Overlay dùng một `MainWindow`.
- [x] Chốt economy, Pomodoro, combo, coin, level và rank.
- [x] Chốt visual direction **Guild Quest Board**.
- [x] Tạo background, ambient sprites và mascot knight bốn state.
- [x] Chuẩn hóa pivot animation, sai số ngang dưới `1 px`, dọc `0 px`.

**Commit gần nhất:** `6de9948 fix: stabilize pixel art animation pivots`

## M01 — Solution foundation và SQLite bootstrap

**Mục tiêu:** Có solution .NET 10 build được, dependency direction đúng và database SQLite tự khởi tạo bằng migration.

### Phạm vi

- [x] Tạo `EasyToDoList.sln`.
- [x] Tạo ba project sản phẩm: `App`, `Core`, `Infrastructure`.
- [x] Tạo ba project test tương ứng.
- [x] Cấu hình `net10.0`/`net10.0-windows`, nullable và warnings-as-errors.
- [x] Thêm EF Core SQLite và migration đầu tiên.
- [x] Tạo database trong thư mục dữ liệu người dùng, không đặt cạnh executable.
- [x] Seed đúng một `PlayerProfile` với EXP/coin bằng `0`.
- [x] Thiết lập composition root tối thiểu để ứng dụng mở được cửa sổ trống.

### Hoàn thành khi

- [x] Core không reference App hoặc Infrastructure.
- [x] Database mới được tạo bằng migration và có đúng một profile.
- [x] `dotnet build -warnaserror` pass.
- [x] `dotnet test` pass.
- [x] Commit: `chore: scaffold WPF solution and SQLite persistence`.

## M02 — Economy, profile, level và rank

**Mục tiêu:** Toàn bộ công thức thưởng/phạt là pure Core logic và được khóa bằng unit test.

### Phạm vi

- [x] Implement `EconomyRules`, `EconomyCalculator`, `RewardAmount`.
- [x] Base EXP: 1/2/7/14+ ngày tương ứng 20/25/50/85.
- [x] Early bonus tối đa 50%, deadline nhận 0 bonus.
- [x] Cancellation penalty 10%, clamp 2–10 EXP.
- [x] Overdue penalty 30%, late recovery 25%.
- [x] Pomodoro EXP/coin tuyến tính theo block 5 phút.
- [x] Implement `PlayerProgress` và rank F–S.
- [x] Dùng `MidpointRounding.AwayFromZero`.

### Hoàn thành khi

- [x] Test toàn bộ boundary 1/2/7/14/30 ngày pass.
- [x] Test level/rank tăng và giảm pass.
- [x] Economy không reference database, clock hoặc WPF.
- [x] `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: add economy and adventurer progression rules`.

## M03 — To-do domain, transaction và reward ledger

**Mục tiêu:** CRUD nghiệp vụ To-do, hoàn thành, hủy và Undo hoạt động nguyên tử với EXP/coin.

### Phạm vi

- [x] Implement `TodoItem`, `TodoStatus`, commands và results.
- [x] Validate title, start date và deadline.
- [x] Implement reward snapshot và schedule edit rules.
- [x] Implement `ITodoService` và các store cụ thể.
- [x] Tạo `RewardTransaction` immutable ledger.
- [x] Tạo `IAppUnitOfWork`/`EfAppUnitOfWork`.
- [x] Complete cập nhật Todo/Profile/Ledger trong một transaction.
- [x] Cancel áp dụng penalty đúng trạng thái.
- [x] Undo trong 5 giây tạo reversal transaction, không xóa ledger cũ.
- [x] Idempotency key ngăn thưởng/phạt trùng.

### Hoàn thành khi

- [x] Save failure rollback toàn bộ Todo/Profile/Ledger.
- [x] Tick/Undo/tick nhanh không cộng trùng.
- [x] Terminal state và invalid transition bị chặn.
- [x] `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: implement transactional todo rewards`.

## M04 — Daily reconciliation, quá hạn và lịch sử

**Mục tiêu:** Trạng thái công việc đúng sau midnight, sleep hoặc nhiều ngày không mở ứng dụng.

### Phạm vi

- [x] Implement `IClock` có thể thay bằng fake clock trong test.
- [x] Lock reward snapshot khi bước sang ngày bắt đầu.
- [x] Chuyển Active → Overdue sau deadline.
- [x] Áp dụng overdue penalty đúng một lần.
- [x] Cung cấp `ReconcileAsync` idempotent cho startup, midnight và resume; wiring Windows thực hiện ở M10.
- [x] Implement query Today và history theo tuần/ngày.
- [x] Lịch sử phân biệt planned date và completion/cancellation date.

### Hoàn thành khi

- [x] Mở ứng dụng sau nhiều ngày không nhân penalty.
- [x] Gọi reconciliation lặp vẫn idempotent.
- [x] Test boundary trước/sau 00:00 pass.
- [x] `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: reconcile daily todo state and history`.

## M05 — Pomodoro state machine và persistence

**Mục tiêu:** Pomodoro chạy chính xác, persist được và không tính thời gian khi máy/app bị gián đoạn.

### Phạm vi

- [x] Implement ba preset 15/3/15, 25/5/20, 50/10/25.
- [x] Validate custom focus 15–60 phút, short break 20–30%, long break 15–30 phút.
- [x] Implement state machine Focus/Break/Paused/Awaiting/Stopped/Skipped.
- [x] Timer dựa trên `PlannedEndAtUtc`, không decrement làm source of truth.
- [x] Start, Pause, Resume, Skip và Stop.
- [x] Snapshot configuration khi bắt đầu phiên.
- [x] Persist session và reward completion nguyên tử.
- [x] Crash/sleep/Exit khôi phục thành Paused và không tự thưởng.

### Hoàn thành khi

- [x] Invalid transition bị chặn.
- [x] Skip/Stop không nhận thưởng.
- [x] Hoàn thành Focus nhận thưởng đúng một lần.
- [x] Hai phiên 25 phút bằng một phiên 50 phút về tổng thưởng.
- [x] `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: add recoverable pomodoro state machine`.

## M06 — WPF shell và medieval design foundation

**Mục tiêu:** Có một cửa sổ WPF chuyển được giữa Dashboard, Overlay và tray với design tokens Guild Quest Board.

### Phạm vi

- [ ] Implement `ShellViewModel`, `ShellDisplayMode`, `OverlayMode`.
- [ ] Một `MainWindow`, không tạo Dashboard/Overlay window riêng.
- [ ] Dashboard không Topmost; Overlay Topmost, compact và draggable.
- [ ] `X` chuyển HiddenToTray; Exit mới đóng tiến trình.
- [ ] Pomodoro service sống xuyên suốt khi đổi mode.
- [ ] Tạo resource dictionaries cho màu, typography, spacing và controls.
- [ ] Áp dụng màu chữ tương phản đã chốt: nâu gần đen trên giấy sáng, kem trên gỗ tối.
- [ ] Tích hợp background Guild Valley ở mức nền, chưa thêm animation.

### Hoàn thành khi

- [ ] Chỉ một mode hiển thị tại một thời điểm.
- [ ] Chuyển mode không tạo lại service/state.
- [ ] Keyboard focus cơ bản hoạt động.
- [ ] Shell tests, `dotnet build -warnaserror` và `dotnet test` pass.
- [ ] Commit: `feat: add guild themed single-window shell`.

## M07 — Dashboard, To-do UI, lịch sử và Settings

**Mục tiêu:** Người dùng quản lý được toàn bộ To-do và xem tiến trình từ Dashboard.

### Phạm vi

- [ ] Today: Active + Overdue, create/edit/cancel/complete.
- [ ] Form hiển thị validation cạnh field.
- [ ] Calendar/history theo tuần và ngày.
- [ ] Profile hiển thị EXP, coin, level và rank.
- [ ] Reward ledger history.
- [ ] Settings JSON versioned trong SQLite.
- [ ] Settings cho âm lượng, animation, Reduced Motion, notification và auto-start.
- [ ] Pomodoro custom validation giải thích lỗi cụ thể.
- [ ] Empty/loading/error states tối thiểu.

### Hoàn thành khi

- [ ] Luồng tạo → sửa → hủy/hoàn thành hoạt động từ UI tới SQLite.
- [ ] App restart vẫn giữ dữ liệu và Settings.
- [ ] Settings JSON lỗi dùng default mà không xóa dữ liệu người dùng.
- [ ] ViewModel tests, `dotnet build -warnaserror` và `dotnet test` pass.
- [ ] Commit: `feat: add dashboard todo history and settings`.

## M08 — Compact Overlay và Pomodoro UI

**Mục tiêu:** Overlay dùng được hằng ngày ở hai chế độ To-do/Pomodoro mà không cần mở Dashboard.

### Phạm vi

- [ ] Overlay To-do hiển thị công việc hiện tại và cho phép tick.
- [ ] Overlay Pomodoro hiển thị phase, thời gian và Start/Pause/Resume/Skip/Stop.
- [ ] Nút mở Dashboard và đổi Overlay mode.
- [ ] Giữ vị trí Overlay giữa các lần mở.
- [ ] Timer UI refresh từ snapshot, không tự quản lý thời gian.
- [ ] Stop có confirmation; Skip Focus không thưởng.
- [ ] Thể hiện rõ Focus, Short Break và Long Break.

### Hoàn thành khi

- [ ] Overlay luôn nổi và vẫn thao tác được khi app khác đang focus.
- [ ] Chuyển Dashboard ↔ Overlay không mất timer hoặc unsaved state hợp lệ.
- [ ] Không có logic economy/Pomodoro trong code-behind.
- [ ] ViewModel tests, `dotnet build -warnaserror` và `dotnet test` pass.
- [ ] Commit: `feat: add compact todo and pomodoro overlay`.

## M09 — Animation hoàn thành, combo, Undo và reward feedback

**Mục tiêu:** Hoàn thành công việc có feedback game hóa nhưng không làm sai hoặc khóa nghiệp vụ.

### Phạm vi

- [ ] Animation crack → glow → pixel burst → collapse trong 700–900 ms.
- [ ] Danh sách chỉ dồn lên sau animation của item tương ứng.
- [ ] Combo window 2 giây và circular countdown.
- [ ] Âm combo tăng bậc nhưng không nhân EXP/coin.
- [ ] Reward toast hiển thị EXP, early bonus và coin từ service result.
- [ ] Undo 5 giây gọi reversal transaction.
- [ ] Tích hợp mascot Celebrate sau completion.
- [ ] Animation/audio chỉ chạy sau database commit.
- [ ] Service failure không phát animation hoặc phần thưởng giả.

### Hoàn thành khi

- [ ] Tick nhiều To-do liên tiếp không khóa input.
- [ ] Combo reset/expire đúng bằng fake clock.
- [ ] Undo khôi phục item và đảo reward đúng một lần.
- [ ] Reduced Motion dùng feedback thay thế, không mất thông tin.
- [ ] `dotnet build -warnaserror` và `dotnet test` pass.
- [ ] Commit: `feat: add completion combo and undo feedback`.

## M10 — Windows lifecycle, tray, notification và startup

**Mục tiêu:** Ứng dụng cư xử đúng như một Windows utility khi chạy nền, sleep/resume và khởi động lại.

### Phạm vi

- [ ] System tray menu: Overlay, Dashboard, Exit.
- [ ] Single-instance; instance thứ hai focus instance hiện có rồi thoát.
- [ ] Sleep/Exit pause Pomodoro trước khi shutdown.
- [ ] Resume chạy reconciliation.
- [ ] Notification cho Pomodoro end, due và Overdue.
- [ ] Notification chỉ hoạt động khi process còn sống.
- [ ] Auto-start setting.
- [ ] Local rolling log không ghi nội dung To-do không cần thiết.

### Hoàn thành khi

- [ ] Startup order: migration → recovery → reconciliation → UI.
- [ ] Tray vẫn giữ timer/notification chạy.
- [ ] Exit không để background process.
- [ ] Manual test sleep/resume, tray và notification trên Windows 11 pass.
- [ ] `dotnet build -warnaserror` và `dotnet test` pass.
- [ ] Commit: `feat: integrate Windows lifecycle and notifications`.

## M11 — Accessibility, visual polish và asset integration

**Mục tiêu:** Hoàn thiện Guild Quest Board UI và tích hợp ambient/mascot mà không làm giảm khả năng sử dụng.

### Phạm vi

- [ ] Tích hợp normalized mascot frames `320×320`.
- [ ] Idle/Focus/Celebrate/Rest đúng trạng thái.
- [ ] Cloud translate, flag 6 fps, grass 4 fps và lá rơi thưa.
- [ ] Reduced Motion dừng ambient và dùng frame `00`.
- [ ] Nearest-neighbor scaling, không blur pixel-art.
- [ ] Kiểm tra 100%/150%/200% DPI.
- [ ] Hoàn thiện tab order, focus visual, accessible name và contrast.
- [ ] Thông tin không phụ thuộc duy nhất vào màu/âm thanh/animation.
- [ ] Thay placeholder audio bằng bộ asset có quyền sử dụng rõ ràng.
- [ ] Ghi nguồn/license cho asset phát hành.

### Hoàn thành khi

- [ ] Mascot/cờ/cỏ không trượt pivot khi loop trong WPF thật.
- [ ] Animation không làm UI thread giật hoặc tăng CPU bất hợp lý.
- [ ] Reduced Motion và muted mode vẫn dùng đủ chức năng.
- [ ] Manual visual/accessibility checklist pass.
- [ ] `dotnet build -c Release -warnaserror` và `dotnet test` pass.
- [ ] Commit: `feat: finish medieval visual and accessibility pass`.

## M12 — Installer và release verification

**Mục tiêu:** Có bản cài đặt Windows 11 x64 tự chứa và checklist phát hành lặp lại được.

### Phạm vi

- [ ] Publish `win-x64` self-contained.
- [ ] Tạo installer Inno Setup.
- [ ] Installer tạo shortcut và uninstall sạch executable.
- [ ] Uninstall không tự xóa database người dùng.
- [ ] Viết README hướng dẫn sử dụng và vị trí dữ liệu.
- [ ] Viết manual release checklist.
- [ ] Test fresh install, upgrade, uninstall và migration.
- [ ] Test offline, tray, notification, sleep/resume và midnight rollover.
- [ ] Test crash recovery, DPI và animation/settings.

### Hoàn thành khi

- [ ] `dotnet test` pass toàn bộ.
- [ ] `dotnet build -c Release -warnaserror` pass.
- [ ] Installer chạy được trên Windows 11 x64 sạch.
- [ ] Không còn lỗi blocker/critical trong release checklist.
- [ ] Commit: `feat: package EasyToDoList MVP`.

---

## Sau MVP — chưa thực hiện

Không đưa các mục sau vào M01–M12:

- Shop và inventory cosmetic.
- Mage/dragon mascot hoàn chỉnh.
- Achievement, daily quest và leaderboard.
- Recurring task, subtask, tag và project.
- Account, cloud sync, ASP.NET Core API và PostgreSQL.
- Web/mobile client.

Các phần này chỉ được thiết kế lại sau khi M12 hoàn thành và MVP đã được sử dụng thử.
