# M03 — To-do Domain, Transaction and Reward Ledger

> Execute this plan with `superpowers:executing-plans` and strict TDD.

**Goal:** Implement To-do CRUD use cases whose status, profile balance and immutable reward ledger are committed atomically in SQLite.

**Spec:** `docs/superpowers/specs/2026-11-11-easy-to-do-list-design.md`

**Architecture:** `TodoItem` owns validation and state transitions in Core. `TodoService` coordinates concrete store interfaces and one unit-of-work boundary. EF Core maps the aggregate and immutable ledger directly; no generic repository or duplicate persistence DTO is introduced. SQLite constraints provide the final idempotency and concurrency guard.

**Scope map:**

- Core callers: future ViewModels consume `ITodoService`, commands and results.
- Core logic: `TodoItem`, clock, reward ledger, store/unit-of-work contracts and `TodoService`.
- Persistence: EF mappings, concrete stores, unit of work and migration.
- Tests/fixtures: aggregate tests, real SQLite transaction tests, migration/bootstrap test.
- Existing behavior at risk: initial database creation and the singleton profile seed must remain compatible.

**Global constraints:**

- Completion/Cancel/Undo update Todo, profile and ledger in one transaction.
- Repeated Complete/Cancel/Undo must never apply a second balance delta.
- Undo is accepted only until exactly five seconds after completion and adds a reversal row instead of deleting history.
- Cancellation and reversal never make profile EXP or coin negative.
- Schedule cannot change after its reward snapshot is locked.
- No UI, reconciliation, history dashboard or shop work belongs to M03.

### Task 1: Domain aggregate and public contracts

**Files:**

- Create `tests/EasyToDoList.Core.Tests/Todos/TodoItemTests.cs`
- Create `src/EasyToDoList.Core/Common/IClock.cs`
- Create `src/EasyToDoList.Core/Common/IAppUnitOfWork.cs`
- Create `src/EasyToDoList.Core/Todos/TodoStatus.cs`
- Create `src/EasyToDoList.Core/Todos/TodoItem.cs`
- Create `src/EasyToDoList.Core/Todos/TodoCommands.cs`
- Create `src/EasyToDoList.Core/Todos/TodoResults.cs`
- Create `src/EasyToDoList.Core/Todos/ITodoService.cs`
- Create `src/EasyToDoList.Core/Todos/ITodoStore.cs`
- Create `src/EasyToDoList.Core/Economy/RewardTransaction.cs`
- Create `src/EasyToDoList.Core/Economy/IRewardStore.cs`
- Create `src/EasyToDoList.Core/Profile/IProfileStore.cs`

**RED:** Test trimmed/non-empty title, deadline boundary, terminal transitions, reward lock, schedule edit before/after lock, completion Undo restoration and overdue/cancellation transitions. Run `dotnet test tests/EasyToDoList.Core.Tests --filter TodoItem --no-restore --disable-build-servers -m:1` and confirm failure because the API does not exist.

**GREEN:** Add the smallest aggregate and immutable contracts satisfying those behaviors. Run the same command and confirm pass.

### Task 2: Transactional service and real SQLite stores

**Files:**

- Create `tests/EasyToDoList.Infrastructure.Tests/Todos/TodoServiceTransactionTests.cs`
- Create `src/EasyToDoList.Core/Todos/TodoService.cs`
- Create `src/EasyToDoList.Infrastructure/Persistence/SqliteTodoStore.cs`
- Create `src/EasyToDoList.Infrastructure/Persistence/SqliteRewardStore.cs`
- Create `src/EasyToDoList.Infrastructure/Persistence/SqliteProfileStore.cs`
- Create `src/EasyToDoList.Infrastructure/Persistence/EfAppUnitOfWork.cs`
- Modify `src/EasyToDoList.Infrastructure/Persistence/AppDbContext.cs`

**RED:** Against a real in-memory SQLite connection, test Create/GetToday, Complete balance+ledger+status, repeated Complete idempotency, Cancel penalty, valid/expired/repeated Undo, tick/Undo/tick, and a SaveChanges interceptor failure rolling back all three state changes. Run `dotnet test tests/EasyToDoList.Infrastructure.Tests --filter TodoServiceTransaction --no-restore --disable-build-servers -m:1`; confirm compile/test failure for missing implementation.

**GREEN:** Implement `TodoService`, tracked stores and `EfAppUnitOfWork`. Use one EF transaction and unique deterministic operation keys. Run the same command and confirm pass.

### Task 3: Migration, regression verification and milestone update

**Files:**

- Modify `tests/EasyToDoList.Infrastructure.Tests/Persistence/DatabaseInitializerTests.cs`
- Create EF migration for TodoItems and RewardTransactions
- Modify `TASKS.md`

**RED:** Extend bootstrap test to assert the two tables, required indexes and unique idempotency constraint exist; run its test and confirm failure before migration.

**GREEN:** Generate/review the migration, mark M03 complete, then run:

- `dotnet test EasyToDoList.sln --no-restore --disable-build-servers -m:1`
- `dotnet build EasyToDoList.sln --no-restore --warnaserror --disable-build-servers -m:1`

Expected: all tests pass and build exits 0 without warnings.

Commit: `feat: implement transactional todo rewards`

## Review focus

- Retry/concurrent clicks cannot duplicate EXP/coin.
- Rollback clears tracked mutations as well as database rows.
- Undo cannot reverse an unrelated or already reversed completion.
- Ledger deltas reconcile with the clamped profile balance.
- EF optimistic concurrency and SQLite uniqueness are actually configured.
