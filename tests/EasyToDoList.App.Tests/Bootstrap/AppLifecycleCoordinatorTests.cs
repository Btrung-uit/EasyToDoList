using EasyToDoList.App.Bootstrap;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Core.Settings;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Tests.Bootstrap;

public sealed class AppLifecycleCoordinatorTests
{
    [Fact]
    public async Task Startup_recovers_then_reconciles_and_midnight_reconciles_once()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        Assert.Equal(new[] { "recover", "reconcile" }, f.Operations);
        await f.Lifecycle.TickAsync();
        Assert.Equal(1, f.Operations.Count(x => x == "reconcile"));
        f.Clock.LocalToday = f.Clock.LocalToday.AddDays(1);
        await f.Lifecycle.TickAsync();
        await f.Lifecycle.TickAsync();
        Assert.Equal(2, f.Operations.Count(x => x == "reconcile"));
    }

    [Fact]
    public async Task Suspend_pauses_before_resume_reconciliation_and_never_ticks_offline()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        f.Pomodoro.Current = Running();
        await f.Lifecycle.SuspendAsync();
        await f.Lifecycle.TickAsync();
        Assert.Contains("pause:SystemSleep", f.Operations);
        Assert.DoesNotContain("tick", f.Operations);
        await f.Lifecycle.ResumeAsync();
        Assert.Equal("reconcile", f.Operations.Last());
        Assert.Equal(PomodoroStatus.Paused, f.Pomodoro.Current!.Status);
    }

    [Fact]
    public async Task Notifications_obey_independent_settings_and_do_not_repeat_or_run_after_stop()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        f.Settings = AppSettings.Default with { DueNotificationsEnabled = false };
        f.Todos.Add(TodoItem.Create(Guid.NewGuid(), "private title", f.Clock.LocalToday, f.Clock.LocalToday, f.Clock.UtcNow, f.Clock.LocalToday));
        f.Todos.Add(TodoItem.Create(Guid.NewGuid(), "private title", f.Clock.LocalToday.AddDays(-2), f.Clock.LocalToday.AddDays(-1), f.Clock.UtcNow, f.Clock.LocalToday));
        f.Pomodoro.Current = Running() with { RemainingSeconds = 0 };
        await f.Lifecycle.TickAsync();
        await f.Lifecycle.TickAsync();
        Assert.Equal(2, f.Notifications.Count);
        Assert.DoesNotContain(f.Notifications, x => x.Contains("private title"));
        f.Settings = AppSettings.Default;
        await f.Lifecycle.TickAsync();
        Assert.Equal(3, f.Notifications.Count);
        f.Lifecycle.Stop();
        f.Clock.LocalToday = f.Clock.LocalToday.AddDays(1);
        await f.Lifecycle.TickAsync();
        Assert.Equal(3, f.Notifications.Count);
    }

    [Fact]
    public async Task Failed_notification_is_retried_without_repeating_committed_pomodoro_reward()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        f.Pomodoro.Current = Running() with { RemainingSeconds = 0 };
        f.FailNotification = true;
        await Assert.ThrowsAsync<IOException>(() => f.Lifecycle.TickAsync());
        f.FailNotification = false;
        await f.Lifecycle.TickAsync();
        Assert.Single(f.Notifications);
        Assert.Single(f.Operations, x => x == "tick");
    }

    [Fact]
    public async Task Resume_recovers_running_session_if_suspend_pause_failed()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        f.Pomodoro.Current = Running();
        f.Pomodoro.FailPause = true;
        await Assert.ThrowsAsync<IOException>(() => f.Lifecycle.SuspendAsync());
        await f.Lifecycle.ResumeAsync();
        Assert.Equal(PomodoroStatus.Paused, f.Pomodoro.Current!.Status);
        Assert.DoesNotContain("tick", f.Operations);
    }

    [Fact]
    public async Task Old_overdue_items_do_not_notify_again_at_each_midnight()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        f.Todos.Add(TodoItem.Create(Guid.NewGuid(), "private", f.Clock.LocalToday.AddDays(-2),
            f.Clock.LocalToday.AddDays(-1), f.Clock.UtcNow, f.Clock.LocalToday));
        await f.Lifecycle.TickAsync();
        f.Clock.LocalToday = f.Clock.LocalToday.AddDays(1);
        await f.Lifecycle.TickAsync();
        Assert.Single(f.Notifications);
    }

    [Fact]
    public async Task Disabled_pomodoro_notification_still_settles_timer_once()
    {
        var f = new Fixture();
        await f.Lifecycle.InitializeAsync();
        f.Settings = f.Settings with { PomodoroNotificationsEnabled = false };
        f.Pomodoro.Current = Running() with { RemainingSeconds = 0 };
        await f.Lifecycle.TickAsync();
        f.Settings = AppSettings.Default;
        await f.Lifecycle.TickAsync();
        Assert.Empty(f.Notifications);
        Assert.Single(f.Operations, x => x == "tick");
    }

    private static PomodoroSnapshot Running() => new(Guid.NewGuid(), PomodoroPhase.Focus, PomodoroStatus.Running,
        PomodoroConfiguration.Standard, 1500, 1200, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(20), null, null, 0, null, null);

    private sealed class Fixture
    {
        public List<string> Operations { get; } = [];
        public List<string> Notifications { get; } = [];
        public List<TodoItem> Todos { get; } = [];
        public AppSettings Settings { get; set; } = AppSettings.Default;
        public bool FailNotification { get; set; }
        public Clock Clock { get; } = new();
        public Pomodoro Pomodoro { get; }
        public AppLifecycleCoordinator Lifecycle { get; }
        public Fixture()
        {
            Pomodoro = new(Operations);
            Lifecycle = new(Pomodoro, new Reconciliation(Operations), Clock,
                () => Task.FromResult<IReadOnlyList<TodoItem>>(Todos), () => Settings,
                (title, body) => { if (FailNotification) throw new IOException(); Notifications.Add(title + body); });
        }
    }
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday { get; set; } = new(2026, 11, 16);
    }
    private sealed class Reconciliation(List<string> operations) : IDailyReconciliationService
    {
        public Task ReconcileAsync(DateOnly date, CancellationToken ct) { operations.Add("reconcile"); return Task.CompletedTask; }
    }
    private sealed class Pomodoro(List<string> operations) : IPomodoroService
    {
        public PomodoroSnapshot? Current { get; set; }
        public bool FailPause { get; set; }
        public Task RecoverAsync(CancellationToken ct) { operations.Add("recover"); if (Current is not null) Current = Current with { Status = PomodoroStatus.Paused }; return Task.CompletedTask; }
        public Task TickAsync(CancellationToken ct) { operations.Add("tick"); Current = Current! with { Status = PomodoroStatus.Awaiting, CompletedAtUtc = DateTimeOffset.UtcNow }; return Task.CompletedTask; }
        public Task PauseAsync(PauseReason reason, CancellationToken ct) { operations.Add($"pause:{reason}"); if (FailPause) throw new IOException(); Current = Current! with { Status = PomodoroStatus.Paused }; return Task.CompletedTask; }
        public Task StartAsync(PomodoroConfiguration c, CancellationToken ct) => throw new NotSupportedException();
        public Task ResumeAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SkipAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task StopAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
