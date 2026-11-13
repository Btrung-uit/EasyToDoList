using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EasyToDoList.Infrastructure.Tests.Pomodoro;

public sealed class PomodoroServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_persists_running_focus_and_configuration_snapshot()
    {
        await using var fixture = await PomodoroFixture.CreateAsync();

        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);

        var saved = await fixture.Db.PomodoroSessions.SingleAsync();
        Assert.Equal(PomodoroPhase.Focus, saved.Phase);
        Assert.Equal(PomodoroStatus.Running, saved.Status);
        Assert.Equal(PomodoroConfiguration.Standard, saved.Configuration);
        Assert.Equal(Now.AddMinutes(25), saved.PlannedEndAtUtc);
    }

    [Fact]
    public async Task Tick_checkpoints_deadline_derived_remaining_time()
    {
        await using var fixture = await PomodoroFixture.CreateAsync();
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddMinutes(5);

        await fixture.Service.TickAsync(CancellationToken.None);

        Assert.Equal(20 * 60, fixture.Service.Current!.RemainingSeconds);
        Assert.Equal(20 * 60, (await fixture.Db.PomodoroSessions.SingleAsync()).RemainingSeconds);
    }

    [Fact]
    public async Task Pause_and_resume_do_not_count_offline_time()
    {
        await using var fixture = await PomodoroFixture.CreateAsync();
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddMinutes(5);
        await fixture.Service.PauseAsync(PauseReason.User, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddHours(2);

        await fixture.Service.ResumeAsync(CancellationToken.None);

        Assert.Equal(20 * 60, fixture.Service.Current!.RemainingSeconds);
        Assert.Equal(Now.AddHours(2).AddMinutes(20), fixture.Service.Current.PlannedEndAtUtc);
    }

    [Fact]
    public async Task Awaiting_phase_uses_started_session_snapshot_not_new_settings()
    {
        await using var fixture = await PomodoroFixture.CreateAsync();
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddMinutes(25);
        await fixture.Service.TickAsync(CancellationToken.None);

        await fixture.Service.StartAsync(PomodoroConfiguration.DeepFocus, CancellationToken.None);

        Assert.Equal(PomodoroPhase.ShortBreak, fixture.Service.Current!.Phase);
        Assert.Equal(5 * 60, fixture.Service.Current.PlannedDurationSeconds);
        Assert.Equal(PomodoroConfiguration.Standard, fixture.Service.Current.Configuration);
    }

    [Fact]
    public async Task Invalid_start_is_rejected_without_adding_another_session()
    {
        await using var fixture = await PomodoroFixture.CreateAsync();
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.StartAsync(PomodoroConfiguration.DeepFocus, CancellationToken.None));

        Assert.Equal(1, await fixture.Db.PomodoroSessions.CountAsync());
    }

    [Fact]
    public async Task Completed_focus_rewards_profile_and_ledger_exactly_once_but_break_does_not()
    {
        await using var fixture = await PomodoroFixture.CreateAsync();
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddMinutes(25);

        await fixture.Service.TickAsync(CancellationToken.None);
        await fixture.Service.TickAsync(CancellationToken.None);
        await fixture.Service.StartAsync(PomodoroConfiguration.DeepFocus, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddMinutes(30);
        await fixture.Service.TickAsync(CancellationToken.None);

        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        var reward = await fixture.Db.RewardTransactions.SingleAsync();
        Assert.Equal(10, profile.TotalExperience);
        Assert.Equal(5, profile.CoinBalance);
        Assert.Equal(RewardSourceType.Pomodoro, reward.SourceType);
        Assert.Equal(RewardReason.Completion, reward.Reason);
        Assert.Equal(PomodoroPhase.Focus, fixture.Service.Current!.Phase);
        Assert.Equal(PomodoroStatus.Awaiting, fixture.Service.Current.Status);
    }

    [Fact]
    public async Task Two_standard_focus_phases_equal_one_deep_focus_reward()
    {
        await using var standard = await PomodoroFixture.CreateAsync();
        await standard.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        standard.Clock.UtcNow = Now.AddMinutes(25);
        await standard.Service.TickAsync(CancellationToken.None);
        await standard.Service.SkipAsync(CancellationToken.None);
        await standard.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        standard.Clock.UtcNow = Now.AddMinutes(50);
        await standard.Service.TickAsync(CancellationToken.None);

        await using var deep = await PomodoroFixture.CreateAsync();
        await deep.Service.StartAsync(PomodoroConfiguration.DeepFocus, CancellationToken.None);
        deep.Clock.UtcNow = Now.AddMinutes(50);
        await deep.Service.TickAsync(CancellationToken.None);

        var standardProfile = await standard.Db.PlayerProfiles.SingleAsync();
        var deepProfile = await deep.Db.PlayerProfiles.SingleAsync();
        Assert.Equal(deepProfile.TotalExperience, standardProfile.TotalExperience);
        Assert.Equal(deepProfile.CoinBalance, standardProfile.CoinBalance);
        Assert.Equal(2, await standard.Db.RewardTransactions.CountAsync());
        Assert.Single(await deep.Db.RewardTransactions.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Skip_and_stop_focus_do_not_reward(bool skip)
    {
        await using var fixture = await PomodoroFixture.CreateAsync();
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);

        if (skip)
        {
            await fixture.Service.SkipAsync(CancellationToken.None);
        }
        else
        {
            await fixture.Service.StopAsync(CancellationToken.None);
        }

        Assert.Empty(await fixture.Db.RewardTransactions.ToListAsync());
        Assert.Equal(0, (await fixture.Db.PlayerProfiles.SingleAsync()).TotalExperience);
    }

    [Fact]
    public async Task Completion_save_failure_rolls_back_session_profile_and_ledger_then_allows_retry()
    {
        var interceptor = new FailingSaveInterceptor();
        await using var fixture = await PomodoroFixture.CreateAsync(interceptor);
        await fixture.Service.StartAsync(PomodoroConfiguration.Standard, CancellationToken.None);
        fixture.Clock.UtcNow = Now.AddMinutes(25);
        interceptor.Enabled = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.TickAsync(CancellationToken.None));
        interceptor.Enabled = false;
        await fixture.Service.TickAsync(CancellationToken.None);

        Assert.Equal(PomodoroStatus.Awaiting, fixture.Service.Current!.Status);
        Assert.Equal(10, (await fixture.Db.PlayerProfiles.SingleAsync()).TotalExperience);
        Assert.Single(await fixture.Db.RewardTransactions.ToListAsync());
    }

    private sealed class PomodoroFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PomodoroFixture(
            SqliteConnection connection,
            AppDbContext db,
            FakeClock clock,
            PomodoroService service)
        {
            _connection = connection;
            Db = db;
            Clock = clock;
            Service = service;
        }

        public AppDbContext Db { get; }

        public FakeClock Clock { get; }

        public PomodoroService Service { get; }

        public static async Task<PomodoroFixture> CreateAsync(SaveChangesInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            var db = new AppDbContext(options.Options);
            await db.Database.EnsureCreatedAsync();
            var clock = new FakeClock { UtcNow = Now, LocalToday = DateOnly.FromDateTime(Now.Date) };
            var service = new PomodoroService(
                new SqlitePomodoroStore(db),
                new SqliteRewardStore(db),
                new SqliteProfileStore(db),
                new EfAppUnitOfWork(db),
                new EconomyCalculator(),
                clock);
            return new PomodoroFixture(connection, db, clock, service);
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
