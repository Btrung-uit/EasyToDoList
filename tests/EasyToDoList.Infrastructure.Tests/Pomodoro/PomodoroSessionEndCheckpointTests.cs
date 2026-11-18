using System.Diagnostics;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Tests.Pomodoro;

public sealed class PomodoroSessionEndCheckpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_end_preserves_focus_or_break_without_counting_offline_time(bool inBreak)
    {
        using var fixture = new Fixture();
        using (var db = fixture.Open())
        {
            var service = fixture.Service(db);
            await service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
            if (inBreak)
            {
                fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(25);
                await service.TickAsync(CancellationToken.None);
                await service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
            }
        }
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddSeconds(73);
        var expected = (inBreak ? 300 : 1500) - 73;
        PomodoroSessionEndCheckpoint.Save(fixture.Path, fixture.Clock.UtcNow);
        PomodoroSessionEndCheckpoint.Save(fixture.Path, fixture.Clock.UtcNow.AddSeconds(10));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddDays(1);
        using var restartedDb = fixture.Open();
        var restarted = fixture.Service(restartedDb);
        await restarted.RecoverAsync(CancellationToken.None);
        Assert.Equal(PomodoroStatus.Paused, restarted.Current!.Status);
        Assert.Equal(PauseReason.ApplicationExit, restarted.Current.PauseReason);
        Assert.Equal(expected, restarted.Current.RemainingSeconds);
        Assert.Equal(inBreak ? PomodoroPhase.ShortBreak : PomodoroPhase.Focus, restarted.Current.Phase);
        Assert.Null(restarted.Current.PlannedEndAtUtc);
        Assert.Equal(inBreak ? 1 : 0, restartedDb.RewardTransactions.Count());
        await restarted.ResumeAsync(CancellationToken.None);
        Assert.Equal(expected, restarted.Current.RemainingSeconds);
    }

    [Fact]
    public async Task Elapsed_running_session_uses_checkpoint_without_creating_offline_reward()
    {
        using var fixture = new Fixture();
        using (var db = fixture.Open())
        {
            var service = fixture.Service(db);
            await service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
            fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(5);
            await service.TickAsync(CancellationToken.None);
        }
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(3);
        PomodoroSessionEndCheckpoint.Save(fixture.Path, fixture.Clock.UtcNow);
        using var saved = fixture.Open();
        Assert.Equal(PomodoroStatus.Paused, saved.PomodoroSessions.Single().Status);
        Assert.Equal(1200, saved.PomodoroSessions.Single().RemainingSeconds);
        Assert.Empty(saved.RewardTransactions);
    }

    [Fact]
    public async Task Existing_user_pause_is_unchanged()
    {
        using var fixture = new Fixture();
        using (var db = fixture.Open())
        {
            var service = fixture.Service(db);
            await service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
            fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(2);
            await service.PauseAsync(PauseReason.User, CancellationToken.None);
        }
        PomodoroSessionEndCheckpoint.Save(fixture.Path, fixture.Clock.UtcNow.AddHours(1));
        using var saved = fixture.Open();
        Assert.Equal(1380, saved.PomodoroSessions.Single().RemainingSeconds);
        Assert.Equal(PauseReason.User, saved.PomodoroSessions.Single().PauseReason);
    }

    [Fact]
    public async Task Busy_database_fails_promptly_and_keeps_previous_checkpoint_recoverable()
    {
        using var fixture = new Fixture();
        using var db = fixture.Open();
        var service = fixture.Service(db);
        await service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(5);
        await service.TickAsync(CancellationToken.None);
        using (var heldTransaction = db.Database.BeginTransaction())
        {
            var elapsed = Stopwatch.StartNew();
            Assert.Throws<SqliteException>(() => PomodoroSessionEndCheckpoint.Save(fixture.Path, fixture.Clock.UtcNow));
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown save blocked for {elapsed.Elapsed}");
        }
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddDays(1);
        using var restartedDb = fixture.Open();
        var restarted = fixture.Service(restartedDb);
        await restarted.RecoverAsync(CancellationToken.None);
        Assert.Equal(PomodoroStatus.Paused, restarted.Current!.Status);
        Assert.Equal(1200, restarted.Current.RemainingSeconds);
        Assert.Empty(restartedDb.RewardTransactions);
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"EasyToDoList-shutdown-{Guid.NewGuid():N}.db");
        public Clock Clock { get; } = new();
        public Fixture() { using var db = Open(); db.Database.EnsureCreated(); }
        public AppDbContext Open() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false }.ToString()).Options);
        public PomodoroService Service(AppDbContext db) => new(new SqlitePomodoroStore(db), new SqliteRewardStore(db),
            new SqliteProfileStore(db), new EfAppUnitOfWork(db), new EconomyCalculator(), Clock);
        public void Dispose() => System.IO.File.Delete(Path);
    }
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday => DateOnly.FromDateTime(UtcNow.DateTime);
    }
}
