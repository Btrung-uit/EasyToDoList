namespace EasyToDoList.Core.Pomodoro;

public enum PomodoroPhase
{
    Focus,
    ShortBreak,
    LongBreak,
}

public enum PomodoroStatus
{
    Running,
    Paused,
    Awaiting,
    Stopped,
    Skipped,
}

public enum PauseReason
{
    User,
    SystemSleep,
    ApplicationExit,
    CrashRecovery,
}

public sealed class PomodoroSession
{
    private PomodoroSession()
    {
    }

    public Guid Id { get; private set; }

    public PomodoroPhase Phase { get; private set; }

    public PomodoroStatus Status { get; private set; }

    public int FocusMinutes { get; private set; }

    public int ShortBreakMinutes { get; private set; }

    public int LongBreakMinutes { get; private set; }

    public int LongBreakInterval { get; private set; }

    public int PlannedDurationSeconds { get; private set; }

    public int RemainingSeconds { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? PlannedEndAtUtc { get; private set; }

    public DateTimeOffset? PausedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public int CompletedFocusCount { get; private set; }

    public DateTimeOffset? RewardAppliedAtUtc { get; private set; }

    public PauseReason? PauseReason { get; private set; }

    public int RowVersion { get; private set; }

    public PomodoroConfiguration Configuration =>
        new(FocusMinutes, ShortBreakMinutes, LongBreakMinutes, LongBreakInterval);

    public static PomodoroSession Start(
        Guid id,
        PomodoroConfiguration configuration,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session ID cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(configuration);
        var durationSeconds = checked(configuration.FocusMinutes * 60);
        return new PomodoroSession
        {
            Id = id,
            Phase = PomodoroPhase.Focus,
            Status = PomodoroStatus.Running,
            FocusMinutes = configuration.FocusMinutes,
            ShortBreakMinutes = configuration.ShortBreakMinutes,
            LongBreakMinutes = configuration.LongBreakMinutes,
            LongBreakInterval = configuration.LongBreakInterval,
            PlannedDurationSeconds = durationSeconds,
            RemainingSeconds = durationSeconds,
            StartedAtUtc = nowUtc,
            PlannedEndAtUtc = nowUtc.AddSeconds(durationSeconds),
        };
    }

    public PomodoroSnapshot GetSnapshot(DateTimeOffset nowUtc) =>
        new(
            Id,
            Phase,
            Status,
            Configuration,
            PlannedDurationSeconds,
            CalculateRemainingSeconds(nowUtc),
            StartedAtUtc,
            PlannedEndAtUtc,
            PausedAtUtc,
            CompletedAtUtc,
            CompletedFocusCount,
            RewardAppliedAtUtc,
            PauseReason);

    public void Pause(DateTimeOffset nowUtc, PauseReason reason)
    {
        EnsureStatus(PomodoroStatus.Running, "Only a running phase can be paused.");
        var remainingSeconds = CalculateRemainingSeconds(nowUtc);
        if (remainingSeconds == 0)
        {
            throw new InvalidOperationException("Elapsed phase must be completed before it can be paused.");
        }

        RemainingSeconds = remainingSeconds;
        PlannedEndAtUtc = null;
        PausedAtUtc = nowUtc;
        PauseReason = reason;
        Status = PomodoroStatus.Paused;
        Touch();
    }

    public void Resume(DateTimeOffset nowUtc)
    {
        EnsureStatus(PomodoroStatus.Paused, "Only a paused phase can be resumed.");
        PlannedEndAtUtc = nowUtc.AddSeconds(RemainingSeconds);
        PausedAtUtc = null;
        PauseReason = null;
        Status = PomodoroStatus.Running;
        Touch();
    }

    public void StartAwaiting(DateTimeOffset nowUtc)
    {
        EnsureStatus(PomodoroStatus.Awaiting, "Only an awaiting phase can be started.");
        StartedAtUtc = nowUtc;
        PlannedEndAtUtc = nowUtc.AddSeconds(RemainingSeconds);
        PausedAtUtc = null;
        CompletedAtUtc = null;
        PauseReason = null;
        Status = PomodoroStatus.Running;
        Touch();
    }

    public PomodoroPhase? CompleteIfElapsed(DateTimeOffset nowUtc)
    {
        EnsureStatus(PomodoroStatus.Running, "Only a running phase can complete.");
        RemainingSeconds = CalculateRemainingSeconds(nowUtc);
        if (RemainingSeconds > 0)
        {
            return null;
        }

        var completedPhase = Phase;
        CompletedAtUtc = nowUtc;
        PlannedEndAtUtc = null;
        if (completedPhase == PomodoroPhase.Focus)
        {
            CompletedFocusCount = checked(CompletedFocusCount + 1);
            SetAwaitingPhase(
                CompletedFocusCount % LongBreakInterval == 0
                    ? PomodoroPhase.LongBreak
                    : PomodoroPhase.ShortBreak);
        }
        else
        {
            SetAwaitingPhase(PomodoroPhase.Focus);
        }

        Touch();
        return completedPhase;
    }

    public void Checkpoint(DateTimeOffset nowUtc)
    {
        EnsureStatus(PomodoroStatus.Running, "Only a running phase can be checkpointed.");
        RemainingSeconds = CalculateRemainingSeconds(nowUtc);
        Touch();
    }

    public void MarkRewardApplied(DateTimeOffset nowUtc)
    {
        if (CompletedFocusCount == 0 || Status != PomodoroStatus.Awaiting)
        {
            throw new InvalidOperationException("A focus reward can only follow a completed focus phase.");
        }

        RewardAppliedAtUtc = nowUtc;
        Touch();
    }

    public void RecoverAsPaused(DateTimeOffset nowUtc)
    {
        EnsureStatus(PomodoroStatus.Running, "Only a running phase needs crash recovery.");
        PlannedEndAtUtc = null;
        PausedAtUtc = nowUtc;
        PauseReason = global::EasyToDoList.Core.Pomodoro.PauseReason.CrashRecovery;
        Status = PomodoroStatus.Paused;
        Touch();
    }

    public void Skip(DateTimeOffset nowUtc)
    {
        EnsureActive("A terminal session cannot be skipped.");
        PlannedEndAtUtc = null;
        PausedAtUtc = null;
        CompletedAtUtc = nowUtc;
        PauseReason = null;
        if (Phase == PomodoroPhase.Focus)
        {
            Status = PomodoroStatus.Skipped;
            RemainingSeconds = 0;
        }
        else
        {
            SetAwaitingPhase(PomodoroPhase.Focus);
        }

        Touch();
    }

    public void Stop(DateTimeOffset nowUtc)
    {
        EnsureActive("A terminal session cannot be stopped.");
        RemainingSeconds = Status == PomodoroStatus.Running
            ? CalculateRemainingSeconds(nowUtc)
            : RemainingSeconds;
        PlannedEndAtUtc = null;
        PausedAtUtc = null;
        CompletedAtUtc = nowUtc;
        PauseReason = null;
        Status = PomodoroStatus.Stopped;
        Touch();
    }

    private int CalculateRemainingSeconds(DateTimeOffset nowUtc)
    {
        if (Status != PomodoroStatus.Running || PlannedEndAtUtc is null)
        {
            return RemainingSeconds;
        }

        var seconds = (PlannedEndAtUtc.Value - nowUtc).TotalSeconds;
        return seconds <= 0 ? 0 : checked((int)Math.Ceiling(seconds));
    }

    private void SetAwaitingPhase(PomodoroPhase phase)
    {
        Phase = phase;
        PlannedDurationSeconds = checked(GetMinutes(phase) * 60);
        RemainingSeconds = PlannedDurationSeconds;
        Status = PomodoroStatus.Awaiting;
    }

    private int GetMinutes(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => FocusMinutes,
        PomodoroPhase.ShortBreak => ShortBreakMinutes,
        PomodoroPhase.LongBreak => LongBreakMinutes,
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private void EnsureStatus(PomodoroStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void EnsureActive(string message)
    {
        if (Status is PomodoroStatus.Stopped or PomodoroStatus.Skipped)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Touch() => RowVersion = checked(RowVersion + 1);
}
