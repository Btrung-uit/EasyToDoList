using EasyToDoList.Core.Common;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Core.Settings;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Bootstrap;

public sealed class AppLifecycleCoordinator(
    IPomodoroService pomodoro,
    IDailyReconciliationService reconciliation,
    IClock clock,
    Func<Task<IReadOnlyList<TodoItem>>> getTodos,
    Func<AppSettings> getSettings,
    Action<string, string> notify)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<(Guid Id, DateOnly Deadline, bool Overdue)> _todoNotifications = [];
    private readonly HashSet<bool> _todoReminderTypes = [];
    private HashSet<Guid> _reminderCandidates = [];
    private DateOnly? _todoReminderDate;
    private DateOnly? _reconciledDate;
    private DateTimeOffset? _lastCompletion;
    private DateTimeOffset? _lastCheckpoint;
    private bool _suspended;
    private bool _stopped;

    public async Task InitializeAsync()
    {
        await pomodoro.RecoverAsync(CancellationToken.None);
        _lastCompletion = pomodoro.Current?.CompletedAtUtc;
        await ReconcileAsync();
        _reminderCandidates = (await getTodos()).Select(todo => todo.Id).ToHashSet();
        _todoReminderDate = clock.LocalToday;
    }

    public Task SuspendAsync()
    {
        // Block ticks immediately, including when persisting the pause fails.
        _suspended = true;
        return RunAsync(async () =>
        {
            if (pomodoro.Current?.Status == PomodoroStatus.Running)
                await pomodoro.PauseAsync(PauseReason.SystemSleep, CancellationToken.None);
        });
    }

    public Task ResumeAsync() => RunAsync(async () =>
    {
        if (pomodoro.Current?.Status == PomodoroStatus.Running)
            await pomodoro.RecoverAsync(CancellationToken.None);
        await ReconcileAsync();
        _suspended = false;
    });

    public Task TickAsync() => RunAsync(async () =>
    {
        if (_suspended) return;
        if (_reconciledDate != clock.LocalToday) await ReconcileAsync();
        if (pomodoro.Current is { Status: PomodoroStatus.Running } snapshot
            && (snapshot.RemainingSeconds <= 0 || _lastCheckpoint is null
                || clock.UtcNow - _lastCheckpoint >= TimeSpan.FromSeconds(30)))
        {
            await pomodoro.TickAsync(CancellationToken.None);
            _lastCheckpoint = clock.UtcNow;
        }

        var settings = getSettings();
        var completedAt = pomodoro.Current?.CompletedAtUtc;
        if (completedAt is not null && completedAt != _lastCompletion
            && pomodoro.Current?.Status == PomodoroStatus.Awaiting)
        {
            if (settings.PomodoroNotificationsEnabled)
                notify("Pomodoro hoàn thành", "Phiên đã kết thúc. Mở EasyToDoList để tiếp tục.");
            _lastCompletion = completedAt;
        }

        if (_todoReminderDate == clock.LocalToday && _todoReminderTypes.Count == 2) return;
        var todos = await getTodos();
        if (_todoReminderDate != clock.LocalToday)
        {
            _todoReminderDate = clock.LocalToday;
            _reminderCandidates = todos.Select(todo => todo.Id).ToHashSet();
            _todoReminderTypes.Clear();
        }
        foreach (var overdue in new[] { false, true })
        {
            var enabled = overdue ? settings.OverdueNotificationsEnabled : settings.DueNotificationsEnabled;
            if (!enabled || _todoReminderTypes.Contains(overdue)) continue;
            var pending = todos.Where(todo => _reminderCandidates.Contains(todo.Id)
                && todo.Status is not (TodoStatus.Completed or TodoStatus.Cancelled)
                && (overdue ? todo.Deadline < clock.LocalToday : todo.Deadline == clock.LocalToday)
                && !_todoNotifications.Contains((todo.Id, todo.Deadline, overdue))).ToArray();
            if (pending.Length > 0)
                notify(overdue ? "Nhiệm vụ quá hạn" : "Nhiệm vụ đến hạn", $"Bạn có {pending.Length} nhiệm vụ cần xem trong EasyToDoList.");
            foreach (var todo in pending) _todoNotifications.Add((todo.Id, todo.Deadline, overdue));
            _todoReminderTypes.Add(overdue);
        }
    });

    public void Stop() => _stopped = true;

    public async Task StopAsync()
    {
        Stop();
        await _gate.WaitAsync();
        _gate.Release();
    }

    private async Task ReconcileAsync()
    {
        var date = clock.LocalToday;
        await reconciliation.ReconcileAsync(date, CancellationToken.None);
        _reconciledDate = date;
    }

    private async Task RunAsync(Func<Task> operation)
    {
        await _gate.WaitAsync();
        try { if (!_stopped) await operation(); }
        finally { _gate.Release(); }
    }
}
