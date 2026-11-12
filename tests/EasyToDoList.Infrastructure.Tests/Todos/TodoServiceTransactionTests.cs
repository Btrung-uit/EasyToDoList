using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EasyToDoList.Infrastructure.Tests.Todos;

public sealed class TodoServiceTransactionTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_update_and_get_today_persist_open_schedule()
    {
        await using var fixture = await TodoFixture.CreateAsync();
        var todayTodo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("  Học EF Core  ", Today, Today),
            CancellationToken.None);
        await fixture.Service.CreateAsync(
            new CreateTodoCommand("Ngày mai", Today.AddDays(1), Today.AddDays(1)),
            CancellationToken.None);

        await fixture.Service.UpdateScheduleAsync(
            new UpdateTodoScheduleCommand(todayTodo.Id, Today, Today.AddDays(1)),
            CancellationToken.None);
        var result = await fixture.Service.GetTodayAsync(Today, CancellationToken.None);

        var saved = Assert.Single(result);
        Assert.Equal("Học EF Core", saved.Title);
        Assert.Equal(Today.AddDays(1), saved.Deadline);
    }

    [Fact]
    public async Task Complete_updates_todo_profile_and_ledger_once()
    {
        await using var fixture = await TodoFixture.CreateAsync();
        var todo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("Task", Today, Today.AddDays(1)),
            CancellationToken.None);

        var first = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);
        var retry = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var savedTodo = await fixture.Db.TodoItems.SingleAsync();
        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        var transaction = await fixture.Db.RewardTransactions.SingleAsync();
        Assert.Equal(TodoStatus.Completed, savedTodo.Status);
        Assert.Equal(38, profile.TotalExperience);
        Assert.Equal(6, profile.CoinBalance);
        Assert.Equal(38, transaction.ExperienceDelta);
        Assert.Equal(6, transaction.CoinDelta);
        Assert.Equal(first.TransactionId, retry.TransactionId);
        Assert.Equal(Now.AddSeconds(5), first.UndoUntilUtc);
    }

    [Fact]
    public async Task Cancel_applies_clamped_penalty_once()
    {
        await using var fixture = await TodoFixture.CreateAsync();
        var todo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("Task", Today, Today.AddDays(1)),
            CancellationToken.None);
        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        profile.TotalExperience = 10;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.CancelAsync(todo.Id, CancellationToken.None);
        await fixture.Service.CancelAsync(todo.Id, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(7, (await fixture.Db.PlayerProfiles.SingleAsync()).TotalExperience);
        var transaction = await fixture.Db.RewardTransactions.SingleAsync();
        Assert.Equal(RewardReason.Cancellation, transaction.Reason);
        Assert.Equal(-3, transaction.ExperienceDelta);
    }

    [Fact]
    public async Task Undo_within_five_seconds_adds_one_reversal_and_restores_todo()
    {
        await using var fixture = await TodoFixture.CreateAsync();
        var todo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("Task", Today, Today.AddDays(1)),
            CancellationToken.None);
        var completion = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddSeconds(5);

        await fixture.Service.UndoCompletionAsync(
            todo.Id,
            completion.TransactionId,
            CancellationToken.None);
        await fixture.Service.UndoCompletionAsync(
            todo.Id,
            completion.TransactionId,
            CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var savedTodo = await fixture.Db.TodoItems.SingleAsync();
        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        var transactions = (await fixture.Db.RewardTransactions.ToListAsync())
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
        Assert.Equal(TodoStatus.Active, savedTodo.Status);
        Assert.Equal(0, profile.TotalExperience);
        Assert.Equal(0, profile.CoinBalance);
        Assert.Equal(2, transactions.Count);
        Assert.Equal(-38, transactions[1].ExperienceDelta);
        Assert.Equal(-6, transactions[1].CoinDelta);
        Assert.Equal(completion.TransactionId, transactions[1].ReversesTransactionId);
    }

    [Fact]
    public async Task Undo_after_five_seconds_is_rejected_without_changes()
    {
        await using var fixture = await TodoFixture.CreateAsync();
        var todo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("Task", Today, Today),
            CancellationToken.None);
        var completion = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddSeconds(5).AddTicks(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.UndoCompletionAsync(
                todo.Id,
                completion.TransactionId,
                CancellationToken.None));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(TodoStatus.Completed, (await fixture.Db.TodoItems.SingleAsync()).Status);
        Assert.Single(await fixture.Db.RewardTransactions.ToListAsync());
    }

    [Fact]
    public async Task Complete_undo_complete_creates_new_reward_without_duplication()
    {
        await using var fixture = await TodoFixture.CreateAsync();
        var todo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("Task", Today, Today),
            CancellationToken.None);
        var first = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);
        await fixture.Service.UndoCompletionAsync(todo.Id, first.TransactionId, CancellationToken.None);

        var second = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        Assert.NotEqual(first.TransactionId, second.TransactionId);
        Assert.Equal(20, profile.TotalExperience);
        Assert.Equal(4, profile.CoinBalance);
        Assert.Equal(3, await fixture.Db.RewardTransactions.CountAsync());
    }

    [Fact]
    public async Task Save_failure_rolls_back_and_clears_tracked_mutations()
    {
        var interceptor = new FailingSaveInterceptor();
        await using var fixture = await TodoFixture.CreateAsync(interceptor);
        var todo = await fixture.Service.CreateAsync(
            new CreateTodoCommand("Task", Today, Today),
            CancellationToken.None);
        interceptor.Enabled = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CompleteAsync(todo.Id, CancellationToken.None));
        interceptor.Enabled = false;
        var completion = await fixture.Service.CompleteAsync(todo.Id, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(TodoStatus.Completed, (await fixture.Db.TodoItems.SingleAsync()).Status);
        Assert.Equal(20, (await fixture.Db.PlayerProfiles.SingleAsync()).TotalExperience);
        Assert.Single(await fixture.Db.RewardTransactions.ToListAsync());
        Assert.NotEqual(Guid.Empty, completion.TransactionId);
    }

    private sealed class TodoFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TodoFixture(
            SqliteConnection connection,
            AppDbContext db,
            FakeClock clock,
            TodoService service)
        {
            _connection = connection;
            Db = db;
            Clock = clock;
            Service = service;
        }

        public AppDbContext Db { get; }

        public FakeClock Clock { get; }

        public TodoService Service { get; }

        public static async Task<TodoFixture> CreateAsync(SaveChangesInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection);
            if (interceptor is not null)
            {
                optionsBuilder.AddInterceptors(interceptor);
            }

            var db = new AppDbContext(optionsBuilder.Options);
            await db.Database.EnsureCreatedAsync();
            var clock = new FakeClock { UtcNow = Now, LocalToday = Today };
            var service = new TodoService(
                new SqliteTodoStore(db),
                new SqliteRewardStore(db),
                new SqliteProfileStore(db),
                new EfAppUnitOfWork(db),
                new EconomyCalculator(),
                clock);
            return new TodoFixture(connection, db, clock, service);
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

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled)
            {
                throw new InvalidOperationException("Injected save failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
