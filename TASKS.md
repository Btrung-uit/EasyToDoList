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
| M06 | WPF shell và medieval design foundation | Hoàn thành | M01, M05 |
| M07 | Dashboard, To-do UI, lịch sử và Settings | Hoàn thành | M04, M06 |
| M08 | Compact Overlay và Pomodoro UI | Hoàn thành | M05, M06 |
| M09 | Animation hoàn thành, combo, Undo và reward feedback | Hoàn thành | M03, M07, M08 |
| M10 | Windows lifecycle, tray, notification và startup | Đã triển khai; chờ kiểm thử Windows thủ công | M04, M05, M06 |
| M11 | Accessibility, visual polish và asset integration | Đã triển khai; chờ kiểm thử visual/accessibility thủ công | M07–M10 |
| M12 | Installer, release verification và tài liệu sử dụng | Chưa bắt đầu | M11 |

Đối chiếu 16/11/2026: M07 và M08 đã merge qua PR #19/#20; M09 đã triển khai ở commit `22d482f` (chưa merge vào `origin/main` tại thời điểm bắt đầu M10). Nhánh M10 kế thừa snapshot M09, không thực hiện merge.

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

- [x] Implement `ShellViewModel`, `ShellDisplayMode`, `OverlayMode`.
- [x] Một `MainWindow`, không tạo Dashboard/Overlay window riêng.
- [x] Dashboard không Topmost; Overlay Topmost, compact và draggable.
- [x] `X` chuyển HiddenToTray; Exit mới đóng tiến trình.
- [x] Pomodoro service sống xuyên suốt khi đổi mode.
- [x] Tạo resource dictionaries cho màu, typography, spacing và controls.
- [x] Áp dụng màu chữ tương phản đã chốt: nâu gần đen trên giấy sáng, kem trên gỗ tối.
- [x] Tích hợp background Guild Valley ở mức nền, chưa thêm animation.

### Hoàn thành khi

- [x] Chỉ một mode hiển thị tại một thời điểm.
- [x] Chuyển mode không tạo lại service/state.
- [x] Keyboard focus cơ bản hoạt động.
- [x] Shell tests, `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: add guild themed single-window shell`.

## M07 — Dashboard, To-do UI, lịch sử và Settings

**Mục tiêu:** Người dùng quản lý được toàn bộ To-do và xem tiến trình từ Dashboard.

### Phạm vi

- [x] Today: Active + Overdue, create/edit/cancel/complete.
- [x] Form hiển thị validation cạnh field.
- [x] Calendar/history theo tuần và ngày.
- [x] Profile hiển thị EXP, coin, level và rank.
- [x] Reward ledger history.
- [x] Settings JSON versioned trong SQLite.
- [x] Settings cho âm lượng, animation, Reduced Motion, notification và auto-start.
- [x] Pomodoro custom validation giải thích lỗi cụ thể.
- [x] Empty/loading/error states tối thiểu.

### Hoàn thành khi

- [x] Luồng tạo → sửa → hủy/hoàn thành hoạt động từ UI tới SQLite.
- [x] App restart vẫn giữ dữ liệu và Settings.
- [x] Settings JSON lỗi dùng default mà không xóa dữ liệu người dùng.
- [x] ViewModel tests, `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: add dashboard todo history and settings`.

## M08 — Compact Overlay và Pomodoro UI

**Mục tiêu:** Overlay dùng được hằng ngày ở hai chế độ To-do/Pomodoro mà không cần mở Dashboard.

### Phạm vi

- [x] Overlay To-do hiển thị công việc hiện tại và cho phép tick.
- [x] Overlay Pomodoro hiển thị phase, thời gian và Start/Pause/Resume/Skip/Stop.
- [x] Nút mở Dashboard và đổi Overlay mode.
- [x] Giữ vị trí Overlay giữa các lần mở.
- [x] Timer UI refresh từ snapshot, không tự quản lý thời gian.
- [x] Stop có confirmation; Skip Focus không thưởng.
- [x] Thể hiện rõ Focus, Short Break và Long Break.

### Hoàn thành khi

- [x] Overlay luôn nổi và vẫn thao tác được khi app khác đang focus.
- [x] Chuyển Dashboard ↔ Overlay không mất timer hoặc unsaved state hợp lệ.
- [x] Không có logic economy/Pomodoro trong code-behind.
- [x] ViewModel tests, `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: add compact todo and pomodoro overlay`.

## M09 — Animation hoàn thành, combo, Undo và reward feedback

**Mục tiêu:** Hoàn thành công việc có feedback game hóa nhưng không làm sai hoặc khóa nghiệp vụ.

### Phạm vi

- [x] Animation crack → glow → pixel burst → collapse trong 700–900 ms.
- [x] Danh sách chỉ dồn lên sau animation của item tương ứng.
- [x] Combo window 2 giây và circular countdown.
- [x] Âm combo tăng bậc nhưng không nhân EXP/coin.
- [x] Reward toast hiển thị EXP, early bonus và coin từ service result.
- [x] Undo 5 giây gọi reversal transaction.
- [x] Tích hợp mascot Celebrate sau completion.
- [x] Animation/audio chỉ chạy sau database commit.
- [x] Service failure không phát animation hoặc phần thưởng giả.

### Hoàn thành khi

- [x] Tick nhiều To-do liên tiếp không khóa input.
- [x] Combo reset/expire đúng bằng fake clock.
- [x] Undo khôi phục item và đảo reward đúng một lần.
- [x] Reduced Motion dùng feedback thay thế, không mất thông tin.
- [x] `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: add completion combo and undo feedback`.

## M10 — Windows lifecycle, tray, notification và startup

**Mục tiêu:** Ứng dụng cư xử đúng như một Windows utility khi chạy nền, sleep/resume và khởi động lại.

### Phạm vi

- [x] System tray menu: Overlay, Dashboard, Exit.
- [x] Single-instance; instance thứ hai focus instance hiện có rồi thoát.
- [x] Sleep/Exit pause Pomodoro trước khi shutdown.
- [x] Resume chạy reconciliation.
- [x] Notification cho Pomodoro end, due và Overdue.
- [x] Notification chỉ hoạt động khi process còn sống.
- [x] Auto-start setting.
- [x] Local rolling log không ghi nội dung To-do không cần thiết.

### Hoàn thành khi

- [x] Startup order: migration → recovery → reconciliation → UI.
- [x] Tray vẫn giữ timer/notification chạy.
- [x] Exit không để background process.
- [ ] Manual test sleep/resume, tray và notification trên Windows 11 pass.
- [x] `dotnet build -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: integrate Windows lifecycle and notifications`.

Kiểm tra ngày 16/11/2026: 192/192 tests pass; build warnings-as-errors không có warning/error. Kiểm thử tự động đã bao phủ SQLite thật, pause/resume, midnight, notification settings/deduplication, IPC single-instance, rolling log và WPF dispatcher/tray. Chưa đánh dấu hoàn thành M10: còn checklist [Windows thủ công](docs/testing/m10-windows-checklist.md). Smoke chạy app riêng đã dừng trước khi launch vì máy đang có một instance EasyToDoList khác; không đóng hoặc thay đổi instance đó. Forced Windows shutdown dùng crash recovery từ checkpoint tối đa khoảng 30 giây; explicit Exit lưu trạng thái trước khi đóng.

Đối chiếu 17/11/2026: 14/14 kiểm tra executable Release thật pass trên Windows 11, gồm tray/double-click, single-instance hidden/minimized và foreground, auto-start qua Save/Exit, `--tray`, Exit khi Focus đang chạy và mở lại Paused giữ nguyên thời gian. Full suite 211/211 tests pass; build warnings-as-errors 0 warning/error. Không còn process sau smoke; record kiểm thử đã dọn riêng và auto-start đã khôi phục. Sleep/Resume và đăng xuất/đăng nhập được người dùng chọn kiểm thử sau. Thông báo đến hạn đã xác nhận bằng ảnh chụp banner Windows thật (UI Automation ban đầu bỏ sót). Chưa xác nhận chạy hết Focus trong tray hoặc qua midnight thật, nên M10 vẫn chưa đóng. Xem checklist để phân biệt proof thật và clock giả.

## M11 — Accessibility, visual polish và asset integration

**Mục tiêu:** Hoàn thiện Guild Quest Board UI và tích hợp ambient/mascot mà không làm giảm khả năng sử dụng.

### Phạm vi

- [x] Tích hợp normalized mascot frames `320×320`.
- [x] Idle/Focus/Celebrate/Rest đúng trạng thái.
- [x] Cloud translate, flag 6 fps, grass 4 fps và lá rơi thưa.
- [x] Reduced Motion dừng ambient và dùng frame `00`.
- [x] Nearest-neighbor scaling, không blur pixel-art.
- [ ] Kiểm tra 100%/150%/200% DPI.
- [x] Hoàn thiện tab order, focus visual, accessible name và contrast.
- [x] Thông tin không phụ thuộc duy nhất vào màu/âm thanh/animation.
- [x] Thay placeholder audio bằng bộ asset có quyền sử dụng rõ ràng.
- [x] Ghi nguồn/license cho asset phát hành.

### Hoàn thành khi

- [ ] Mascot/cờ/cỏ không trượt pivot khi loop trong WPF thật.
- [ ] Animation không làm UI thread giật hoặc tăng CPU bất hợp lý.
- [x] Reduced Motion và muted mode vẫn dùng đủ chức năng.
- [ ] Manual visual/accessibility checklist pass.
- [x] `dotnet build -c Release -warnaserror` và `dotnet test` pass.
- [x] Commit: `feat: finish medieval visual and accessibility pass`.

Kiểm chứng triển khai và phần còn lại: [M11 visual/accessibility checklist](docs/testing/m11-visual-accessibility-checklist.md). Ảnh 96/144/192 DPI là mô phỏng raster; chưa thay thế kiểm thử DPI màn hình thật, Narrator, chất lượng loa và profiling Windows ổn định. Không đánh dấu hoàn thành M11 trước khi các kiểm tra thủ công pass.

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

## Sửa lỗi sau kiểm thử người dùng — 17/11/2026

- [x] Tên nhiệm vụ, ngày bắt đầu, hạn chót và Lưu có cùng chiều rộng và cao 40 DIP.
- [x] Hoàn thành nhiệm vụ đang sửa đưa form về tạo mới; reload bỏ trạng thái sửa nếu nhiệm vụ đã đóng, không làm mất bản nháp của nhiệm vụ khác.
- [x] Feedback hoàn thành/EXP/coin/Hoàn tác nằm trong một toast góc dưới bên phải, không chiếm hàng layout hoặc làm xuất hiện scrollbar của Dashboard; Overlay chừa nút điều hướng.
- [x] Hoàn tác vẫn có hạn 5 giây và đảo ledger đúng một lần; complete thất bại giữ nguyên form sửa.
- [x] Regression SQLite tái hiện Sửa → Xong/Hủy → Lưu cùng tên hoặc tên khác; kiểm tra WPF đo bốn ô, viewport, vị trí toast và keyboard Undo.
- [x] Overlay rộng tối đa 280 DIP, tự co chiều cao theo nội dung; giảm padding/khoảng cách, danh sách dài chỉ cuộn bên trong. Pomodoro dưới 240 DIP khi không có lỗi; hàng báo lỗi rỗng không chiếm chỗ.
- [x] Overlay xuyên thấu 50% bằng native window alpha, gồm thanh tiêu đề; rõ khi hover hoặc dùng bàn phím. Focus từ nút bấm chuột không ngăn mờ lại khi rời cửa sổ. Pomodoro chỉ hiện Bắt đầu / Tạm dừng+Bỏ qua / Tiếp tục+Dừng theo trạng thái.
- [x] Footer Overlay chỉ còn Mở Dashboard và Ẩn; X vẫn ẩn vào tray theo lựa chọn của người dùng. Thoát hẳn qua tray → Exit.
- [x] Feedback Overlay là popup ngoài cửa sổ, không tăng chiều cao hoặc che nút; dùng cùng nội dung với toast Dashboard. Popup theo cửa sổ khi kéo, tự đổi phía ở mép dưới, đóng khi chuyển Dashboard/ẩn tray/hết hạn Hoàn tác. Smoke script M10 dùng tray Exit thay nút Thoát đã bỏ.
- [x] Chuyển Dashboard ↔ Overlay lưu/khôi phục vị trí, kích thước và trạng thái phóng to riêng của Dashboard; Overlay luôn về Normal. Không có bounds cũ thì Dashboard mở giữa màn hình; chuyển qua tray hoặc lặp nhiều lần vẫn giữ bounds. Chặn lưu/clamp tọa độ trung gian khi đang chuyển chế độ.

- [x] Ẩn phần thưởng sớm khi bằng 0; vẫn hiển thị bonus dương và tổng EXP/coin.
- [x] Bỏ trang Hồ sơ và bảng lịch sử nhận EXP/coin khỏi UI; giữ thông tin tiến trình bên phải và ledger phục vụ thưởng/Hoàn tác, không xóa dữ liệu.
- [x] Pomodoro mặc định hiển thị preset 25/5, 45/10, 50/10; chi tiết nằm trong Cài đặt nâng cao, đóng sẵn. Preset đặt nghỉ dài lần lượt 20/20/25 phút sau 4 phiên; cấu hình nâng cao giữ validation hiện có, áp dụng cho phiên bắt đầu tiếp theo.
- [x] Bỏ hai dòng mô tả preset; lựa chọn hiện tại có nền vàng, viền đậm và trạng thái accessibility. Chỉnh focus/nghỉ ngắn trong nâng cao cập nhật lựa chọn tương ứng.
- [x] Header bỏ nút Overlay/Thoát; nút Thu gọn thành Overlay ở cột phải thay Mở đồng hồ. Bỏ nhãn phase/status khỏi sidebar và Overlay; tên accessibility của đồng hồ giữ phase cùng thời gian còn lại. X vẫn ẩn tray; thoát hẳn qua tray → Exit.

- [x] Regression CI dùng tọa độ Windows runner để phân biệt chạm mép với chồng lấn có diện tích; vẫn phát hiện chồng lấn 1 pixel.

Kiểm chứng: 225/225 tests pass ở Debug và Release; build Debug/Release warnings-as-errors đều 0 warning/error. Runtime dùng SQLite tạm, không thay đổi database cá nhân. Đã kiểm tra hover/leave thật ở client và thanh tiêu đề, focus bàn phím, các trạng thái nút, preset và đóng/mở nâng cao; native alpha 128/255 và pixel màn hình khi thay nền đỏ/xanh chứng minh xuyên thấu thật. Popup ngoài HWND Overlay, theo khi kéo, tránh mép dưới, dự phòng trái/phải khi thiếu chỗ trên/dưới, đóng khi ẩn và đổi chế độ. Binding/accessibility và ảnh UI không có lỗi. Smoke script M10 đã đổi locator theo UI mới và kiểm tra cú pháp; không chạy lại trên dữ liệu cá nhân. Vị trí/giới hạn dùng monitor hiện tại với chuyển đổi pixel/DIP; máy chỉ có một màn hình nên chưa xác minh thực tế trên màn hình phụ. Không thay đổi schema, cách tính EXP/coin hay thời gian Undo.

## Dashboard Quest Journal — 18/11/2026

- [x] Giữ WPF, pixel art và assets; header gọn, ba cột ưu tiên nhiệm vụ, menu có icon/active và thu gọn dưới 1080 DIP.
- [x] Nhập nhanh Enter, lịch mở rộng, xác nhận Hủy, trạng thái quest/empty, toast lưu thành công; giữ Hoàn tác và phần thưởng cũ.
- [x] Daily progress dùng dữ liệu lịch sử thật; completion/Undo/reload/đổi ngày và snapshot tải đồng thời được kiểm thử.
- [x] EXP bar dùng ngưỡng cấp hiện tại; Pomodoro sidebar dùng chung ViewModel/service với Overlay.
- [x] Kiểm tra WPF runtime ở 900×600 và kích thước chuẩn, nội dung dài/cuộn, keyboard focus, điều hướng, Pomodoro/Overlay, binding và SQLite tạm.

Kiểm chứng snapshot cuối: 228/228 tests pass (App 81, Core 99, Infrastructure 48); build warnings-as-errors 0 warning/error. Reviewer phát hiện race tải lại/completion; đã có regression đỏ→xanh và sửa bằng gate chung cho snapshot. Detector UI trả `[]`; ảnh runtime đã xem trực quan. Có hai lần kiểm tra chuột desktop 75 ms thất bại; focused và full run cuối đều pass, test giữ diagnostic tọa độ/HWND. Chưa thay thế kiểm thử DPI màn hình thật, Narrator hoặc nhiều màn hình; M10/M11 vẫn giữ trạng thái kiểm thử thủ công đang mở. Chi tiết, danh sách file và cách chạy: [Dashboard Quest Journal](docs/testing/dashboard-quest-journal.md).

## App logo — 18/11/2026

- [x] Tích hợp PNG trong suốt do người dùng cung cấp vào header 52×52 DIP, giữ chiều cao header.
- [x] ICO 7 kích thước dùng cho executable, cửa sổ/taskbar và tray; icon tray được giải phóng khi Exit.
- [x] Release 228/228 tests pass, build warnings-as-errors 0 warning/error; xem ảnh WPF và kiểm tra layout 900×600. Debug bị instance đang chạy khóa executable nên kiểm chứng Release, không đóng app cá nhân. Nguồn asset: [branding](assets/branding/README.md).

## Sau MVP — chưa thực hiện

Không đưa các mục sau vào M01–M12:

- Shop và inventory cosmetic.
- Mage/dragon mascot hoàn chỉnh.
- Achievement, daily quest và leaderboard.
- Recurring task, subtask, tag và project.
- Account, cloud sync, ASP.NET Core API và PostgreSQL.
- Web/mobile client.

Các phần này chỉ được thiết kế lại sau khi M12 hoàn thành và MVP đã được sử dụng thử.
