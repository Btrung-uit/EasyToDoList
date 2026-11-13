using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Tests.Todos;

public sealed class DailyReconciliationTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reconcile_on_start_date_locks_snapshot_and_activates_todo()
    {
        await using var fixture = await ReconciliationFixture.CreateAsync(Today.AddDays(-1));
        var todo = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Task", Today, Today.AddDays(1)),
            CancellationToken.None);

        await fixture.Reconciliation.ReconcileAsync(Today, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.TodoItems.SingleAsync(item => item.Id == todo.Id);
        Assert.Equal(TodoStatus.Active, saved.Status);
        Assert.Equal(Today, saved.RewardStartDate);
        Assert.Equal(Today.AddDays(1), saved.RewardDeadline);
        Assert.Equal(2, saved.RewardDurationDays);
        Assert.Equal(25, saved.BaseExperience);
        Assert.Equal(Now, saved.RewardLockedAtUtc);
    }

    [Fact]
    public async Task Reconcile_at_deadline_stays_active_then_next_day_penalizes_once()
    {
        await using var fixture = await ReconciliationFixture.CreateAsync(Today);
        var todo = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Task", Today, Today),
            CancellationToken.None);
        await fixture.SetExperienceAsync(100);

        await fixture.Reconciliation.ReconcileAsync(Today, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddDays(1);
        await fixture.Reconciliation.ReconcileAsync(Today.AddDays(1), CancellationToken.None);
        await fixture.Reconciliation.ReconcileAsync(Today.AddDays(1), CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.TodoItems.SingleAsync(item => item.Id == todo.Id);
        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        var transaction = await fixture.Db.RewardTransactions.SingleAsync();
        Assert.Equal(TodoStatus.Overdue, saved.Status);
        Assert.Equal(Now.AddDays(1), saved.OverduePenaltyAppliedAtUtc);
        Assert.Equal(94, profile.TotalExperience);
        Assert.Equal(RewardReason.OverduePenalty, transaction.Reason);
        Assert.Equal(-6, transaction.ExperienceDelta);
    }

    [Fact]
    public async Task Reconcile_after_many_closed_days_uses_original_schedule_and_one_penalty()
    {
        await using var fixture = await ReconciliationFixture.CreateAsync(Today.AddDays(-1));
        var todo = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Long task", Today, Today.AddDays(6)),
            CancellationToken.None);
        await fixture.SetExperienceAsync(100);
        fixture.Clock.UtcNow = Now.AddDays(20);

        await fixture.Reconciliation.ReconcileAsync(Today.AddDays(20), CancellationToken.None);
        await fixture.Reconciliation.ReconcileAsync(Today.AddDays(21), CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.TodoItems.SingleAsync(item => item.Id == todo.Id);
        Assert.Equal(TodoStatus.Overdue, saved.Status);
        Assert.Equal(Today, saved.RewardStartDate);
        Assert.Equal(Today.AddDays(6), saved.RewardDeadline);
        Assert.Equal(7, saved.RewardDurationDays);
        Assert.Equal(50, saved.BaseExperience);
        Assert.Equal(85, (await fixture.Db.PlayerProfiles.SingleAsync()).TotalExperience);
        Assert.Single(await fixture.Db.RewardTransactions.ToListAsync());
    }

    [Fact]
    public async Task Reconcile_from_stale_context_remains_idempotent()
    {
        var databaseName = $"EasyToDoList-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        await using var first = await ReconciliationFixture.CreateAsync(Today, connectionString);
        var todo = await first.Todos.CreateAsync(
            new CreateTodoCommand("Task", Today, Today),
            CancellationToken.None);
        await first.SetExperienceAsync(100);
        await using var second = await ReconciliationFixture.CreateAsync(Today, connectionString);
        first.Clock.UtcNow = Now.AddDays(1);
        second.Clock.UtcNow = Now.AddDays(1);

        await second.Reconciliation.ReconcileAsync(Today.AddDays(1), CancellationToken.None);
        await first.Reconciliation.ReconcileAsync(Today.AddDays(1), CancellationToken.None);

        first.Db.ChangeTracker.Clear();
        Assert.Equal(TodoStatus.Overdue, (await first.Db.TodoItems.SingleAsync(item => item.Id == todo.Id)).Status);
        Assert.Equal(94, (await first.Db.PlayerProfiles.SingleAsync()).TotalExperience);
        Assert.Single(await first.Db.RewardTransactions.ToListAsync());
    }

    [Fact]
    public async Task Reconcile_skips_candidate_moved_to_future_before_transaction()
    {
        var databaseName = $"EasyToDoList-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        await using var first = await ReconciliationFixture.CreateAsync(Today.AddDays(-1), connectionString);
        var todo = await first.Todos.CreateAsync(
            new CreateTodoCommand("Rescheduled", Today, Today.AddDays(1)),
            CancellationToken.None);
        await using var second = await ReconciliationFixture.CreateAsync(Today.AddDays(-1), connectionString);
        var innerStore = new SqliteTodoStore(first.Db);
        var interleavingStore = new InterleavingTodoStore(
            innerStore,
            () => second.Todos.UpdateScheduleAsync(
                new UpdateTodoScheduleCommand(todo.Id, Today.AddDays(5), Today.AddDays(6)),
                CancellationToken.None));
        var reconciliation = new DailyReconciliationService(
            interleavingStore,
            new SqliteRewardStore(first.Db),
            new SqliteProfileStore(first.Db),
            new EfAppUnitOfWork(first.Db),
            new EconomyCalculator(),
            first.Clock);

        await reconciliation.ReconcileAsync(Today, CancellationToken.None);

        first.Db.ChangeTracker.Clear();
        var saved = await first.Db.TodoItems.SingleAsync(item => item.Id == todo.Id);
        Assert.Equal(TodoStatus.Scheduled, saved.Status);
        Assert.Equal(Today.AddDays(5), saved.StartDate);
        Assert.Null(saved.RewardLockedAtUtc);
    }

    [Fact]
    public async Task Reconcile_ignores_completed_and_cancelled_todos()
    {
        await using var fixture = await ReconciliationFixture.CreateAsync(Today);
        var completed = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Done", Today, Today),
            CancellationToken.None);
        var cancelled = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Cancelled", Today, Today),
            CancellationToken.None);
        await fixture.Todos.CompleteAsync(completed.Id, CancellationToken.None);
        await fixture.Todos.CancelAsync(cancelled.Id, CancellationToken.None);
        var transactionCount = await fixture.Db.RewardTransactions.CountAsync();
        fixture.Clock.UtcNow = Now.AddDays(10);

        await fixture.Reconciliation.ReconcileAsync(Today.AddDays(10), CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(TodoStatus.Completed, (await fixture.Db.TodoItems.SingleAsync(item => item.Id == completed.Id)).Status);
        Assert.Equal(TodoStatus.Cancelled, (await fixture.Db.TodoItems.SingleAsync(item => item.Id == cancelled.Id)).Status);
        Assert.Equal(transactionCount, await fixture.Db.RewardTransactions.CountAsync());
        Assert.DoesNotContain(
            await fixture.Db.RewardTransactions.ToListAsync(),
            transaction => transaction.Reason == RewardReason.OverduePenalty);
    }

    [Fact]
    public async Task GetToday_after_reconciliation_returns_active_and_overdue_only()
    {
        await using var fixture = await ReconciliationFixture.CreateAsync(Today);
        var active = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Active", Today, Today.AddDays(1)),
            CancellationToken.None);
        var overdue = await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Overdue", Today.AddDays(-2), Today.AddDays(-1)),
            CancellationToken.None);
        await fixture.Todos.CreateAsync(
            new CreateTodoCommand("Future", Today.AddDays(1), Today.AddDays(2)),
            CancellationToken.None);

        await fixture.Reconciliation.ReconcileAsync(Today, CancellationToken.None);
        var result = await fixture.Todos.GetTodayAsync(Today, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, todo => todo.Id == active.Id && todo.Status == TodoStatus.Active);
        Assert.Contains(result, todo => todo.Id == overdue.Id && todo.Status == TodoStatus.Overdue);
    }

    [Fact]
    public async Task GetToday_returns_fresh_status_reconciled_by_another_context()
    {
        var databaseName = $"EasyToDoList-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        await using var first = await ReconciliationFixture.CreateAsync(Today, connectionString);
        var todo = await first.Todos.CreateAsync(
            new CreateTodoCommand("Task", Today, Today),
            CancellationToken.None);
        await using var second = await ReconciliationFixture.CreateAsync(Today, connectionString);
        second.Clock.UtcNow = Now.AddDays(1);
        await second.Reconciliation.ReconcileAsync(Today.AddDays(1), CancellationToken.None);

        var result = await first.Todos.GetTodayAsync(Today.AddDays(1), CancellationToken.None);

        var saved = Assert.Single(result, item => item.Id == todo.Id);
        Assert.Equal(TodoStatus.Overdue, saved.Status);
        Assert.NotNull(saved.OverduePenaltyAppliedAtUtc);
    }

    private sealed class ReconciliationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ReconciliationFixture(
            SqliteConnection connection,
            AppDbContext db,
            FakeClock clock,
            TodoService todos,
            DailyReconciliationService reconciliation)
        {
            _connection = connection;
            Db = db;
            Clock = clock;
            Todos = todos;
            Reconciliation = reconciliation;
        }

        public AppDbContext Db { get; }

        public FakeClock Clock { get; }

        public TodoService Todos { get; }

        public DailyReconciliationService Reconciliation { get; }

        public static async Task<ReconciliationFixture> CreateAsync(
            DateOnly localToday,
            string connectionString = "Data Source=:memory:")
        {
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var clock = new FakeClock { UtcNow = Now, LocalToday = localToday };
            var todoStore = new SqliteTodoStore(db);
            var rewardStore = new SqliteRewardStore(db);
            var profileStore = new SqliteProfileStore(db);
            var unitOfWork = new EfAppUnitOfWork(db);
            var economy = new EconomyCalculator();
            return new ReconciliationFixture(
                connection,
                db,
                clock,
                new TodoService(todoStore, rewardStore, profileStore, unitOfWork, economy, clock),
                new DailyReconciliationService(
                    todoStore,
                    rewardStore,
                    profileStore,
                    unitOfWork,
                    economy,
                    clock));
        }

        public async Task SetExperienceAsync(long experience)
        {
            var profile = await Db.PlayerProfiles.SingleAsync();
            profile.TotalExperience = experience;
            await Db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }

        public DateOnly LocalToday { get; set; }
    }

    private sealed class InterleavingTodoStore(
        ITodoStore inner,
        Func<Task> afterCandidateRead) : ITodoStore
    {
        public Task AddAsync(TodoItem todo, CancellationToken cancellationToken) =>
            inner.AddAsync(todo, cancellationToken);

        public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            inner.GetAsync(id, cancellationToken);

        public async Task<IReadOnlyList<Guid>> GetReconciliationCandidateIdsAsync(
            DateOnly localDate,
            CancellationToken cancellationToken)
        {
            var ids = await inner.GetReconciliationCandidateIdsAsync(localDate, cancellationToken);
            await afterCandidateRead();
            return ids;
        }

        public Task<IReadOnlyList<TodoItem>> GetTodayAsync(
            DateOnly localDate,
            CancellationToken cancellationToken) =>
            inner.GetTodayAsync(localDate, cancellationToken);
    }
}
