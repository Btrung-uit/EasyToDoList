using System.IO;
using EasyToDoList.App.Bootstrap;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Core.Settings;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.App.Tests.Bootstrap;

public sealed class LifecyclePersistenceTests
{
    [Fact]
    public async Task Background_timer_rewards_once_and_sleep_preserves_remaining_time_across_contexts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EasyToDoList-lifecycle-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "test.db");
        try
        {
            await new DatabaseInitializer(path).InitializeAsync(CancellationToken.None);
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var pomoDb = new AppDbContext(options);
            await using var todoDb = new AppDbContext(options);
            var clock = new Clock();
            var economy = new EconomyCalculator();
            var pomodoro = new SerializedPomodoroService(new PomodoroService(new SqlitePomodoroStore(pomoDb),
                new SqliteRewardStore(pomoDb), new SqliteProfileStore(pomoDb), new EfAppUnitOfWork(pomoDb), economy, clock));
            var todos = new SqliteTodoStore(todoDb);
            var notifications = new List<string>();
            var lifecycle = new AppLifecycleCoordinator(pomodoro,
                new DailyReconciliationService(todos, new SqliteRewardStore(todoDb), new SqliteProfileStore(todoDb),
                    new EfAppUnitOfWork(todoDb), economy, clock), clock,
                () => todos.GetTodayAsync(clock.LocalToday, CancellationToken.None), () => AppSettings.Default,
                (title, _) => notifications.Add(title));
            await lifecycle.InitializeAsync();
            await pomodoro.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            await lifecycle.SuspendAsync();
            Assert.Equal(1200, pomodoro.Current!.RemainingSeconds);
            clock.UtcNow = clock.UtcNow.AddDays(2);
            await lifecycle.ResumeAsync();
            await lifecycle.TickAsync();
            Assert.Equal(1200, pomodoro.Current!.RemainingSeconds);
            Assert.Equal(PomodoroStatus.Paused, pomodoro.Current.Status);
            Assert.Empty(notifications);
            await pomodoro.ResumeAsync(CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddMinutes(20);
            await lifecycle.TickAsync();
            await lifecycle.TickAsync();
            Assert.Single(notifications);
            Assert.Single(await todoDb.RewardTransactions.AsNoTracking().ToListAsync());
            Assert.Equal(PomodoroStatus.Awaiting, pomodoro.Current!.Status);
            await lifecycle.StopAsync();
            await lifecycle.TickAsync();
            Assert.Single(notifications);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday => DateOnly.FromDateTime(UtcNow.DateTime);
    }
}
