# M04 Reconciliation and History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Todo status, reward snapshots and overdue penalties correct across local-day changes, and expose a weekly calendar/history query.

**Architecture:** `DailyReconciliationService` processes candidate Todo IDs one at a time through the existing unit-of-work so each Todo/profile/ledger update is atomic and retry-safe. `SqliteTodoHistoryQuery` returns immutable calendar entries; open Todos are projected onto planned days, while terminal Todos use their local completion/cancellation day and retain planned dates.

**Tech Stack:** C#/.NET 10, EF Core 10, SQLite, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-11-easy-to-do-list-design.md`

## Global Constraints

- Reward snapshot is locked from the saved schedule when reconciliation first sees `localDate >= StartDate`.
- A Todo becomes Overdue only after `localDate > Deadline`, never on the deadline itself.
- Overdue penalty is `round(30% × B)`, clamped by profile balance, and applied at most once.
- Todo/profile/ledger mutation is atomic per Todo; retries use a unique deterministic key.
- Completed and Cancelled are terminal and reconciliation must not change or penalize them.
- Windows event wiring for startup, midnight and resume remains in M10; M04 provides the idempotent entry point those triggers call.

## Review Focus

- App reopened many days late must lock the original snapshot and apply exactly one penalty.
- Repeated and cross-context reconciliation must not duplicate ledger entries or EXP loss.
- Exactly-at-deadline and first-day-after-deadline boundaries must choose Active versus Overdue correctly.
- Completed/Cancelled before reconciliation must remain terminal and unpenalized.
- Week history must convert UTC event timestamps with the selected local time zone and retain planned dates.

---

### Task 1: Idempotent daily reconciliation

**Files:**

- Create: `src/EasyToDoList.Core/Todos/IDailyReconciliationService.cs`
- Create: `src/EasyToDoList.Core/Todos/DailyReconciliationService.cs`
- Modify: `src/EasyToDoList.Core/Todos/TodoItem.cs`
- Modify: `src/EasyToDoList.Core/Todos/ITodoStore.cs`
- Modify: `src/EasyToDoList.Infrastructure/Persistence/SqliteTodoStore.cs`
- Test: `tests/EasyToDoList.Infrastructure.Tests/Todos/DailyReconciliationTests.cs`

**Interfaces:**

- Consumes: existing `ITodoStore`, `IRewardStore`, `IProfileStore`, `IAppUnitOfWork`, `EconomyCalculator`, `IClock`.
- Produces: `Task ReconcileAsync(DateOnly localDate, CancellationToken cancellationToken)`.

- [ ] Write real SQLite tests for start-day lock, deadline boundary, multi-day reopen, repeated call, cross-context call, and terminal Todo exclusion.
- [ ] Run `dotnet test tests/EasyToDoList.Infrastructure.Tests/EasyToDoList.Infrastructure.Tests.csproj --filter DailyReconciliation --no-restore --disable-build-servers -m:1` and verify failure because the service API is missing.
- [ ] Add aggregate transition methods, candidate-ID query and service. Use key `todo-overdue:{todoId:N}:{rewardDeadline:yyyy-MM-dd}`; create a zero-delta ledger row when EXP is already zero so the event remains auditable.
- [ ] Run the same command and verify all reconciliation tests pass.

### Task 2: Weekly calendar and history query

**Files:**

- Create: `src/EasyToDoList.Core/Todos/ITodoHistoryQuery.cs`
- Create: `src/EasyToDoList.Infrastructure/Persistence/SqliteTodoHistoryQuery.cs`
- Test: `tests/EasyToDoList.Infrastructure.Tests/Todos/TodoHistoryQueryTests.cs`

**Interfaces:**

- Consumes: `AppDbContext`, a `TimeZoneInfo` selected by the composition root (defaults to `TimeZoneInfo.Local`).
- Produces: `Task<IReadOnlyList<TodoHistoryEntry>> GetWeekAsync(DateOnly weekStart, CancellationToken cancellationToken)` where each entry has `Date`, planned dates, status and optional actual terminal date.

- [ ] Write SQLite tests for all five statuses, inclusive seven-day boundary, multi-day planned projection, completion/cancellation event dates outside the planned range, and explicit UTC time-zone conversion.
- [ ] Run `dotnet test tests/EasyToDoList.Infrastructure.Tests/EasyToDoList.Infrastructure.Tests.csproj --filter TodoHistoryQuery --no-restore --disable-build-servers -m:1` and verify failure because the query API is missing.
- [ ] Implement immutable result records and a no-tracking query. Filter/project in memory because SQLite cannot reliably group `DateTimeOffset` by arbitrary local time zone.
- [ ] Run the same command and verify all history tests pass.

### Task 3: Milestone verification and documentation

**Files:**

- Modify: `TASKS.md`

- [ ] Mark implemented M04 scope complete and explicitly point Windows lifecycle wiring to M10.
- [ ] Run `dotnet test EasyToDoList.sln --no-restore --disable-build-servers -m:1`; expect every test to pass.
- [ ] Run `dotnet build EasyToDoList.sln --no-restore --warnaserror --disable-build-servers -m:1`; expect 0 warnings and 0 errors.
- [ ] Run `git diff --check`.
- [ ] Commit with `feat: reconcile daily todo state and history`.
