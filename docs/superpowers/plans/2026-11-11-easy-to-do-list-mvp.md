# EasyToDoList MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây ứng dụng Windows offline gồm Dashboard/Overlay, To-do gamification và Pomodoro theo đặc tả đã duyệt.

**Architecture:** Modular monolith một tiến trình gồm WPF App, Core độc lập UI và Infrastructure cho SQLite/Windows. Mọi thay đổi trạng thái cùng EXP/coin được commit nguyên tử; animation/audio chỉ chạy sau commit.

**Tech Stack:** .NET 10 LTS, WPF (`net10.0-windows`), SQLite/EF Core 10, CommunityToolkit.Mvvm, xUnit, Inno Setup

**Spec:** `docs/superpowers/specs/2026-11-11-easy-to-do-list-design.md`

**Architecture:** `docs/architecture/system-design.md`

## Global Constraints

- Hỗ trợ chính thức Windows 11 x64; Windows 10 22H2 chỉ best-effort; bản phát hành self-contained.
- Chạy offline, không tài khoản, cloud, PostgreSQL hoặc kết nối mạng trong MVP.
- Chỉ một `MainWindow`; Dashboard, Overlay và HiddenToTray là mode của cùng shell.
- Toàn bộ economy và Pomodoro rules nằm trong Core, không nằm ở ViewModel/code-behind.
- State nghiệp vụ và reward ledger phải ghi cùng SQLite transaction, có idempotency key.
- Hiệu ứng và âm thanh chạy sau commit; tắt chúng không được thay đổi kết quả nghiệp vụ.
- Không thêm shop, inventory, recurring task, tag, project, achievement hoặc generic repository.

## Review Focus

1. Ứng dụng đóng qua nhiều ngày rồi mở lại: mỗi To-do chỉ khóa reward/phạt quá hạn đúng một lần — Task 4 có integration test.
2. Người dùng tick/Undo/tick rất nhanh: profile, ledger và trạng thái không cộng thưởng hai lần — Task 3 có concurrency/idempotency tests.
3. Máy sleep hoặc Exit giữa Pomodoro: thời gian offline không bị tính và không tự trao thưởng — Task 5 và Task 9 có recovery tests.
4. Database write lỗi sau khi UI nhận thao tác: không chạy animation và không lệch profile với To-do — Task 3 và Task 8 có rollback tests.
5. Windows DPI/Reduced Motion/tắt âm thanh: chức năng vẫn dùng được và thông tin không chỉ thể hiện bằng hiệu ứng — Task 8 và Task 10 có ViewModel/manual checks.

---

### Task 1: Solution foundation và SQLite bootstrap

**Files:**
- Create: `EasyToDoList.sln`
- Create: `src/EasyToDoList.App/EasyToDoList.App.csproj`
- Create: `src/EasyToDoList.Core/EasyToDoList.Core.csproj`
- Create: `src/EasyToDoList.Infrastructure/EasyToDoList.Infrastructure.csproj`
- Create: `tests/EasyToDoList.Core.Tests/EasyToDoList.Core.Tests.csproj`
- Create: `tests/EasyToDoList.Infrastructure.Tests/EasyToDoList.Infrastructure.Tests.csproj`
- Create: `tests/EasyToDoList.App.Tests/EasyToDoList.App.Tests.csproj`
- Create: `src/EasyToDoList.Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Persistence/DatabaseInitializerTests.cs`

**Interfaces:**
- Produces: `AppDbContext`, `DatabaseInitializer.InitializeAsync(CancellationToken)` và project references theo dependency rule.

- [ ] **Step 1: Scaffold solution và project references**

Tạo ba project sản phẩm và ba project test; App reference Core + Infrastructure, Infrastructure reference Core, Core không reference project khác. Target Core `net10.0`, App `net10.0-windows` với WPF, bật nullable và warnings-as-errors.

- [ ] **Step 2: Viết integration test thất bại cho database mới**

`DatabaseInitializerTests.Initialize_creates_database_and_single_profile()` phải dùng file SQLite tạm, gọi initializer và assert database tồn tại cùng đúng một profile ID `1` có EXP/coin bằng `0`.

- [ ] **Step 3: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/EasyToDoList.Infrastructure.Tests --filter Initialize_creates_database_and_single_profile`
Expected: FAIL vì `AppDbContext`/initializer chưa tồn tại.

- [ ] **Step 4: Implement bootstrap tối thiểu**

Tạo `AppDbContext`, singleton `PlayerProfileRow` tạm thời và `DatabaseInitializer.InitializeAsync`; cấu hình SQLite file qua connection string được inject. Tạo migration đầu tiên, không dùng `EnsureCreated` trong production.

- [ ] **Step 5: Verify foundation**

Run: `dotnet test`
Expected: PASS. Run: `dotnet build -warnaserror`
Expected: build thành công không warning.

- [ ] **Step 6: Commit**

`git commit -m "chore: scaffold WPF solution and SQLite persistence"`

### Task 2: Economy, profile, level và rank

**Files:**
- Create: `src/EasyToDoList.Core/Economy/EconomyRules.cs`
- Create: `src/EasyToDoList.Core/Economy/EconomyCalculator.cs`
- Create: `src/EasyToDoList.Core/Economy/RewardAmount.cs`
- Create: `src/EasyToDoList.Core/Profile/PlayerProgress.cs`
- Create: `src/EasyToDoList.Core/Profile/AdventurerRank.cs`
- Create: `tests/EasyToDoList.Core.Tests/Economy/EconomyCalculatorTests.cs`
- Create: `tests/EasyToDoList.Core.Tests/Profile/PlayerProgressTests.cs`

**Interfaces:**
- Produces: `EconomyCalculator` methods trong architecture doc; `PlayerProgress.FromExperience(int)` trả level, progress-to-next-level và rank.

- [ ] **Step 1: Viết failing theory cho mọi boundary economy**

Assert base EXP cho 1/2/7/14/30 ngày là 20/25/50/85/85; early bonus ngày đầu tối đa 50%, ngày cuối 0; cancel clamp 2–10; overdue 30%; recovery 25%; Pomodoro 15/25/40/50/60 phút trả đúng EXP 6/10/16/20/24 và coin 3/5/8/10/12.

- [ ] **Step 2: Viết failing theory cho level/rank**

Pin các mốc đầu/cuối rank F–S và assert EXP âm được clamp về level 1, 0 EXP.

- [ ] **Step 3: Chạy tests để xác nhận FAIL**

Run: `dotnet test tests/EasyToDoList.Core.Tests --filter "Economy|PlayerProgress"`
Expected: FAIL vì types chưa tồn tại.

- [ ] **Step 4: Implement pure rules**

Implement signatures đã ghi trong `docs/architecture/system-design.md`; mọi midpoint dùng `MidpointRounding.AwayFromZero`, không truy cập clock/database.

- [ ] **Step 5: Verify và commit**

Run: `dotnet test tests/EasyToDoList.Core.Tests`
Expected: PASS.

`git commit -m "feat: add economy and adventurer progression rules"`

### Task 3: To-do aggregate, reward ledger và use cases

**Files:**
- Create: `src/EasyToDoList.Core/Common/IClock.cs`
- Create: `src/EasyToDoList.Core/Todos/TodoItem.cs`
- Create: `src/EasyToDoList.Core/Todos/TodoStatus.cs`
- Create: `src/EasyToDoList.Core/Todos/TodoCommands.cs`
- Create: `src/EasyToDoList.Core/Todos/TodoResults.cs`
- Create: `src/EasyToDoList.Core/Todos/ITodoService.cs`
- Create: `src/EasyToDoList.Core/Todos/ITodoStore.cs`
- Create: `src/EasyToDoList.Core/Todos/TodoService.cs`
- Create: `src/EasyToDoList.Core/Economy/RewardTransaction.cs`
- Create: `src/EasyToDoList.Core/Economy/IRewardStore.cs`
- Create: `src/EasyToDoList.Core/Common/IAppUnitOfWork.cs`
- Create: `src/EasyToDoList.Core/Profile/IProfileStore.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/SqliteTodoStore.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/SqliteRewardStore.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/EfAppUnitOfWork.cs`
- Modify: `src/EasyToDoList.Infrastructure/Persistence/AppDbContext.cs`
- Create: `tests/EasyToDoList.Core.Tests/Todos/TodoItemTests.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Todos/TodoServiceTransactionTests.cs`

**Interfaces:**
- Consumes: `EconomyCalculator`, `IClock`, `AppDbContext`.
- Produces: `ITodoService` signatures từ architecture doc và `TodoCompletionResult(TodoItem Todo, RewardAmount Reward, Guid TransactionId, DateTimeOffset UndoUntilUtc)`.

- [ ] **Step 1: Viết failing domain tests**

Test title rỗng, deadline trước start, invalid terminal transition, reward snapshot lock và deadline edit rules.

- [ ] **Step 2: Viết failing integration tests cho transaction**

Test Complete cập nhật Todo/Profile/Ledger nguyên tử; cùng completion idempotency key không thưởng hai lần; injected save failure rollback mọi thay đổi; Undo trong 5 giây tạo reversal và tick lại chỉ thưởng một lần mới.

- [ ] **Step 3: Chạy tests để xác nhận FAIL**

Run: `dotnet test --filter "TodoItem|TodoServiceTransaction"`
Expected: FAIL vì aggregate/store/service chưa tồn tại.

- [ ] **Step 4: Implement aggregate và SQLite transaction**

Thực hiện Create/UpdateSchedule/Complete/Undo/Cancel/GetToday. `EfAppUnitOfWork` sở hữu EF Core transaction chung cho Todo/Profile/Ledger; không tạo generic repository.

- [ ] **Step 5: Verify và commit**

Run: `dotnet test --filter "TodoItem|TodoServiceTransaction"`
Expected: PASS.

`git commit -m "feat: implement transactional todo rewards"`

### Task 4: Daily reconciliation và history queries

**Files:**
- Create: `src/EasyToDoList.Core/Todos/IDailyReconciliationService.cs`
- Create: `src/EasyToDoList.Core/Todos/DailyReconciliationService.cs`
- Create: `src/EasyToDoList.Core/Todos/ITodoHistoryQuery.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/SqliteTodoHistoryQuery.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Todos/DailyReconciliationTests.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Todos/TodoHistoryQueryTests.cs`

**Interfaces:**
- Consumes: `ITodoStore`, `IRewardStore`, `IClock`, `EconomyCalculator`.
- Produces: `ReconcileAsync(DateOnly, CancellationToken)` và `GetWeekAsync(DateOnly weekStart, CancellationToken)`.

- [ ] **Step 1: Viết failing reconciliation tests**

Cover app đóng qua nhiều ngày, gọi reconcile lặp, To-do bắt đầu/overdue cùng batch, cancellation đã có penalty và local midnight boundary.

- [ ] **Step 2: Viết failing history tests**

Assert tuần trả Scheduled/Active/Completed/Overdue/Cancelled đúng ngày, Completed dùng ngày hoàn thành nhưng vẫn giữ planned date.

- [ ] **Step 3: Chạy tests để xác nhận FAIL**

Run: `dotnet test --filter "DailyReconciliation|TodoHistoryQuery"`
Expected: FAIL.

- [ ] **Step 4: Implement idempotent reconciliation và query**

Mỗi Todo được xử lý transactionally; overdue key là `todo-overdue:{todoId}:{rewardDeadline}`; reward snapshot khóa theo ngày bắt đầu đã lưu.

- [ ] **Step 5: Verify và commit**

Run: `dotnet test --filter "DailyReconciliation|TodoHistoryQuery"`
Expected: PASS.

`git commit -m "feat: reconcile daily todo state and history"`

### Task 5: Pomodoro validation, state machine và persistence

**Files:**
- Create: `src/EasyToDoList.Core/Pomodoro/PomodoroConfiguration.cs`
- Create: `src/EasyToDoList.Core/Pomodoro/PomodoroSession.cs`
- Create: `src/EasyToDoList.Core/Pomodoro/PomodoroSnapshot.cs`
- Create: `src/EasyToDoList.Core/Pomodoro/IPomodoroService.cs`
- Create: `src/EasyToDoList.Core/Pomodoro/IPomodoroStore.cs`
- Create: `src/EasyToDoList.Core/Pomodoro/PomodoroService.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/SqlitePomodoroStore.cs`
- Modify: `src/EasyToDoList.Infrastructure/Persistence/AppDbContext.cs`
- Create: `tests/EasyToDoList.Core.Tests/Pomodoro/PomodoroConfigurationTests.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Pomodoro/PomodoroServiceTests.cs`

**Interfaces:**
- Consumes: `IClock`, `IRewardStore`, `EconomyCalculator`.
- Produces: `IPomodoroService` signatures từ architecture doc và immutable `PomodoroSnapshot`.

- [ ] **Step 1: Viết failing validation/state tests**

Pin ba preset, custom ratios, boundaries 15/60 phút, short break 20–30%, long break 15–30 và 3/4 cycles. Test invalid transition, Skip/Stop không thưởng, complete focus thưởng đúng một lần.

- [ ] **Step 2: Viết failing recovery tests**

Test Pause persist remaining; fake sleep/Exit rồi resume không trừ offline time; crash để lại Running được startup recovery chuyển Paused và không thưởng.

- [ ] **Step 3: Chạy tests để xác nhận FAIL**

Run: `dotnet test --filter Pomodoro`
Expected: FAIL.

- [ ] **Step 4: Implement state machine và persistence**

Timer dùng `PlannedEndAtUtc`; `TickAsync` chỉ hoàn tất phase khi đang Running. Settings thay đổi không mutate configuration snapshot của session.

- [ ] **Step 5: Verify và commit**

Run: `dotnet test --filter Pomodoro`
Expected: PASS.

`git commit -m "feat: add recoverable pomodoro state machine"`

### Task 6: WPF shell, Dashboard/Overlay và tray modes

**Files:**
- Create: `src/EasyToDoList.App/App.xaml`
- Create: `src/EasyToDoList.App/App.xaml.cs`
- Create: `src/EasyToDoList.App/Shell/MainWindow.xaml`
- Create: `src/EasyToDoList.App/Shell/MainWindow.xaml.cs`
- Create: `src/EasyToDoList.App/Shell/ShellViewModel.cs`
- Create: `src/EasyToDoList.App/Shell/ShellDisplayMode.cs`
- Create: `src/EasyToDoList.App/Shell/WindowModeController.cs`
- Create: `src/EasyToDoList.Infrastructure/Windows/SystemTrayService.cs`
- Create: `tests/EasyToDoList.App.Tests/Shell/ShellViewModelTests.cs`

**Interfaces:**
- Consumes: singleton `IPomodoroService`, `ISystemTrayService`.
- Produces: `ShowDashboardCommand`, `ShowOverlayCommand`, `HideToTrayCommand`, `ExitCommand`; shell mode state.

- [ ] **Step 1: Viết failing shell tests**

Test chỉ một mode active; chuyển mode giữ cùng Pomodoro snapshot; `X` chọn HiddenToTray; Exit gọi Pause trước shutdown.

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/EasyToDoList.App.Tests --filter ShellViewModel`
Expected: FAIL.

- [ ] **Step 3: Implement shell tối thiểu**

Một `MainWindow` swap Dashboard/Overlay content và áp dụng `Topmost` chỉ ở Overlay. Code-behind chỉ chuyển lifecycle/window events vào ViewModel/controller.

- [ ] **Step 4: Verify và commit**

Run: `dotnet test tests/EasyToDoList.App.Tests --filter ShellViewModel`
Expected: PASS. Mở app thủ công và xác nhận Dashboard/Overlay/tray không tạo window thứ hai.

`git commit -m "feat: add single-window dashboard and overlay shell"`

### Task 7: Dashboard features và Settings

**Files:**
- Create: `src/EasyToDoList.App/Features/Today/TodayView.xaml`
- Create: `src/EasyToDoList.App/Features/Today/TodayViewModel.cs`
- Create: `src/EasyToDoList.App/Features/Calendar/CalendarView.xaml`
- Create: `src/EasyToDoList.App/Features/Calendar/CalendarViewModel.cs`
- Create: `src/EasyToDoList.App/Features/Pomodoro/PomodoroView.xaml`
- Create: `src/EasyToDoList.App/Features/Pomodoro/PomodoroViewModel.cs`
- Create: `src/EasyToDoList.App/Features/Profile/ProfileView.xaml`
- Create: `src/EasyToDoList.App/Features/Profile/ProfileViewModel.cs`
- Create: `src/EasyToDoList.App/Features/Settings/SettingsView.xaml`
- Create: `src/EasyToDoList.App/Features/Settings/SettingsViewModel.cs`
- Create: `src/EasyToDoList.App/Shell/Overlay/OverlayTodoView.xaml`
- Create: `src/EasyToDoList.App/Shell/Overlay/OverlayPomodoroView.xaml`
- Create: `src/EasyToDoList.Core/Settings/AppSettings.cs`
- Create: `src/EasyToDoList.Core/Settings/ISettingsStore.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/JsonSettingsStore.cs`
- Create: `tests/EasyToDoList.App.Tests/Features/DashboardViewModelTests.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Settings/JsonSettingsStoreTests.cs`

**Interfaces:**
- Consumes: Todo/history/Pomodoro services và `ISettingsStore`.
- Produces: Dashboard tabs và persisted, versioned `AppSettings`.

- [ ] **Step 1: Viết failing ViewModel/settings tests**

Test Today load Active+Overdue, field validation, Calendar week mapping, Profile level/rank, invalid Pomodoro settings có message cụ thể, corrupted Settings JSON dùng default mà không ghi đè file lỗi.

- [ ] **Step 2: Chạy tests để xác nhận FAIL**

Run: `dotnet test --filter "DashboardViewModel|JsonSettingsStore"`
Expected: FAIL.

- [ ] **Step 3: Implement dashboard tối thiểu**

Tạo năm tab theo spec và hai compact Overlay view dùng chung ViewModel/service state; command async không block UI, validation cạnh field; không thêm chart/analytics hoặc shop screen.

- [ ] **Step 4: Verify và commit**

Run: `dotnet test`
Expected: PASS.

`git commit -m "feat: add dashboard and persistent settings"`

### Task 8: Completion animation, combo, audio và Undo presentation

**Files:**
- Create: `src/EasyToDoList.App/Presentation/Animation/TodoCompletionAnimator.cs`
- Create: `src/EasyToDoList.App/Presentation/Combo/ComboTracker.cs`
- Create: `src/EasyToDoList.App/Presentation/Combo/ComboViewModel.cs`
- Create: `src/EasyToDoList.App/Presentation/Undo/UndoCoordinator.cs`
- Create: `src/EasyToDoList.App/Presentation/Audio/AudioCoordinator.cs`
- Modify: `src/EasyToDoList.App/Features/Today/TodayView.xaml`
- Modify: `src/EasyToDoList.App/Features/Today/TodayViewModel.cs`
- Create: `tests/EasyToDoList.App.Tests/Presentation/ComboTrackerTests.cs`
- Create: `tests/EasyToDoList.App.Tests/Presentation/CompletionFlowTests.cs`

**Interfaces:**
- Consumes: `TodoCompletionResult`, `ITodoService.UndoCompletionAsync`, settings và `IClock`/presentation timer abstraction.
- Produces: 700–900 ms completion state, 2-second combo state, 5-second Undo state.

- [ ] **Step 1: Viết failing timing/flow tests**

Test combo increment/reset/expire và circular progress 2 giây bằng fake clock; Undo hết hạn; service failure không animator/audio/combo; Reduced Motion và muted audio vẫn remove item sau success; nhiều completion không khóa input.

- [ ] **Step 2: Chạy tests để xác nhận FAIL**

Run: `dotnet test tests/EasyToDoList.App.Tests --filter "ComboTracker|CompletionFlow"`
Expected: FAIL.

- [ ] **Step 3: Implement presentation effects**

Giữ placeholder asset nhất quán; sequence state là crack → glow → pixel burst → collapse. ViewModel chỉ phát effect sau `CompleteAsync` thành công và dùng reward từ result.

- [ ] **Step 4: Verify và commit**

Run: `dotnet test tests/EasyToDoList.App.Tests`
Expected: PASS. Manual check ở 100%/150%/200% DPI và Reduced Motion.

`git commit -m "feat: add completion combo and undo feedback"`

### Task 9: Windows lifecycle, notification và startup integration

**Files:**
- Create: `src/EasyToDoList.Core/Common/IPowerStateService.cs`
- Create: `src/EasyToDoList.Core/Common/ISystemNotificationService.cs`
- Create: `src/EasyToDoList.Core/Common/IStartupRegistrationService.cs`
- Create: `src/EasyToDoList.Infrastructure/Windows/PowerStateService.cs`
- Create: `src/EasyToDoList.Infrastructure/Windows/SystemNotificationService.cs`
- Create: `src/EasyToDoList.Infrastructure/Windows/StartupRegistrationService.cs`
- Create: `src/EasyToDoList.Infrastructure/Windows/SingleInstanceCoordinator.cs`
- Create: `src/EasyToDoList.App/Bootstrap/AppLifecycleCoordinator.cs`
- Create: `tests/EasyToDoList.App.Tests/Bootstrap/AppLifecycleCoordinatorTests.cs`
- Create: `tests/EasyToDoList.Infrastructure.Tests/Windows/SingleInstanceCoordinatorTests.cs`

**Interfaces:**
- Consumes: `IPomodoroService`, `IDailyReconciliationService`, Settings.
- Produces: startup/midnight/resume reconciliation, suspend pause, notification routing.

- [ ] **Step 1: Viết failing lifecycle tests**

Test startup order migration → recovery → reconciliation → show UI; suspend pause; resume reconcile; Exit không để background notification; tray vẫn cho timer/notification chạy; Pomodoro end/due/overdue obey từng notification setting; instance thứ hai chuyển focus rồi thoát.

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/EasyToDoList.App.Tests --filter AppLifecycleCoordinator`
Expected: FAIL.

- [ ] **Step 3: Implement adapters và coordinator**

Adapter chỉ chuyển Windows events; coordinator gọi Core. Notification obey từng setting và chỉ chạy khi process sống.

- [ ] **Step 4: Verify và commit**

Run: `dotnet test`
Expected: PASS. Manual check suspend/resume, tray và startup registration trên Windows 11; Windows 10 chỉ kiểm tra best-effort nếu có môi trường.

`git commit -m "feat: integrate Windows lifecycle and notifications"`

### Task 10: Final medieval skin, packaging và release verification

**Files:**
- Create: `src/EasyToDoList.App/Resources/Styles/Colors.xaml`
- Create: `src/EasyToDoList.App/Resources/Styles/Typography.xaml`
- Create: `src/EasyToDoList.App/Resources/Styles/Controls.xaml`
- Create: `src/EasyToDoList.App/Resources/Images/`
- Create: `src/EasyToDoList.App/Resources/Audio/`
- Create: `installer/EasyToDoList.iss`
- Create: `docs/testing/manual-release-checklist.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: toàn bộ MVP.
- Produces: self-contained x64 installer và release checklist có thể lặp lại.

- [ ] **Step 1: Thay placeholder bằng asset tối thiểu có license rõ ràng**

Bộ tối thiểu gồm background/frame Dashboard+Overlay, icon rank F–S, Pomodoro focus/rest animation, crack/burst frames và completion/combo/tick/bell audio. Ghi nguồn/license trong `Resources/ASSET-LICENSES.md`.

- [ ] **Step 2: Hoàn tất accessibility và keyboard pass**

Kiểm tra tab order, focus visual, accessible name, text contrast, Reduced Motion, muted mode và thông tin reward/status bằng chữ.

- [ ] **Step 3: Tạo installer self-contained x64**

Publish `win-x64` self-contained; installer cài executable/resources, tạo shortcut và uninstall sạch nhưng không tự xóa database người dùng.

- [ ] **Step 4: Chạy automated verification**

Run: `dotnet test`
Expected: PASS. Run: `dotnet build -c Release -warnaserror`
Expected: PASS.

- [ ] **Step 5: Chạy manual release checklist**

Cover fresh install/upgrade/uninstall, SQLite migration, Dashboard/Overlay/tray, sleep/resume, midnight rollover, notification, offline use, 100/150/200% DPI, animation/audio settings và crash recovery.

- [ ] **Step 6: Commit**

`git commit -m "feat: package EasyToDoList MVP"`

## Execution order

Thực hiện tuần tự Task 1–6 vì chúng khóa interfaces và state. Task 7–9 có thể tách nhánh sau Task 6 nhưng nên merge từng task để tránh xung đột App bootstrap. Task 10 chỉ bắt đầu khi toàn bộ automated tests của Task 1–9 pass.

