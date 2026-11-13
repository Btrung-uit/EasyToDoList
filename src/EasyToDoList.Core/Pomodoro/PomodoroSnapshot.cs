namespace EasyToDoList.Core.Pomodoro;

public sealed record PomodoroSnapshot(
    Guid Id,
    PomodoroPhase Phase,
    PomodoroStatus Status,
    PomodoroConfiguration Configuration,
    int PlannedDurationSeconds,
    int RemainingSeconds,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? PlannedEndAtUtc,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int CompletedFocusCount,
    DateTimeOffset? RewardAppliedAtUtc,
    PauseReason? PauseReason);
