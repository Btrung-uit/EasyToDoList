using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.Core.Tests.Pomodoro;

public sealed class PomodoroSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_creates_running_focus_with_configuration_snapshot()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);

        var snapshot = session.GetSnapshot(Now.AddSeconds(30));
        Assert.Equal(PomodoroPhase.Focus, snapshot.Phase);
        Assert.Equal(PomodoroStatus.Running, snapshot.Status);
        Assert.Equal(25 * 60 - 30, snapshot.RemainingSeconds);
        Assert.Equal(Now.AddMinutes(25), snapshot.PlannedEndAtUtc);
        Assert.Equal(PomodoroConfiguration.Standard, snapshot.Configuration);
    }

    [Fact]
    public void Pause_and_resume_preserve_remaining_time_without_counting_offline_time()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);

        session.Pause(Now.AddMinutes(5), PauseReason.User);
        var paused = session.GetSnapshot(Now.AddHours(2));
        session.Resume(Now.AddHours(2));

        Assert.Equal(PomodoroStatus.Paused, paused.Status);
        Assert.Equal(20 * 60, paused.RemainingSeconds);
        Assert.Null(paused.PlannedEndAtUtc);
        Assert.Equal(PauseReason.User, paused.PauseReason);
        Assert.Equal(Now.AddHours(2).AddMinutes(20), session.PlannedEndAtUtc);
    }

    [Fact]
    public void Exact_focus_deadline_advances_to_awaiting_short_break()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);

        var completed = session.CompleteIfElapsed(Now.AddMinutes(25));

        Assert.Equal(PomodoroPhase.Focus, completed);
        Assert.Equal(PomodoroPhase.ShortBreak, session.Phase);
        Assert.Equal(PomodoroStatus.Awaiting, session.Status);
        Assert.Equal(5 * 60, session.RemainingSeconds);
        Assert.Equal(1, session.CompletedFocusCount);
    }

    [Fact]
    public void Fourth_focus_advances_to_long_break()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);
        CompleteFocusAndBreak(session, Now);
        CompleteFocusAndBreak(session, Now.AddHours(1));
        CompleteFocusAndBreak(session, Now.AddHours(2));

        session.StartAwaiting(Now.AddHours(3));
        var completed = session.CompleteIfElapsed(Now.AddHours(3).AddMinutes(25));

        Assert.Equal(PomodoroPhase.Focus, completed);
        Assert.Equal(PomodoroPhase.LongBreak, session.Phase);
        Assert.Equal(4, session.CompletedFocusCount);
    }

    [Fact]
    public void Completing_break_advances_to_awaiting_focus_without_incrementing_count()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);
        session.CompleteIfElapsed(Now.AddMinutes(25));
        session.StartAwaiting(Now.AddMinutes(26));

        var completed = session.CompleteIfElapsed(Now.AddMinutes(31));

        Assert.Equal(PomodoroPhase.ShortBreak, completed);
        Assert.Equal(PomodoroPhase.Focus, session.Phase);
        Assert.Equal(PomodoroStatus.Awaiting, session.Status);
        Assert.Equal(1, session.CompletedFocusCount);
    }

    [Fact]
    public void Skip_focus_terminates_but_skip_break_awaits_focus()
    {
        var focus = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);
        focus.Skip(Now.AddMinutes(1));
        var breakSession = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);
        breakSession.CompleteIfElapsed(Now.AddMinutes(25));
        breakSession.StartAwaiting(Now.AddMinutes(26));

        breakSession.Skip(Now.AddMinutes(27));

        Assert.Equal(PomodoroStatus.Skipped, focus.Status);
        Assert.Equal(PomodoroPhase.Focus, breakSession.Phase);
        Assert.Equal(PomodoroStatus.Awaiting, breakSession.Status);
    }

    [Fact]
    public void Stop_terminates_active_session()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);

        session.Stop(Now.AddMinutes(1));

        Assert.Equal(PomodoroStatus.Stopped, session.Status);
        Assert.Null(session.PlannedEndAtUtc);
    }

    [Fact]
    public void Configuration_snapshot_is_not_mutated_by_external_configuration()
    {
        var original = new PomodoroConfiguration(25, 5, 20, 4);
        var session = PomodoroSession.Start(Guid.NewGuid(), original, Now);

        var laterSettings = new PomodoroConfiguration(50, 10, 25, 4);

        Assert.NotEqual(laterSettings, session.Configuration);
        Assert.Equal(original, session.Configuration);
    }

    [Fact]
    public void Invalid_transitions_are_rejected()
    {
        var running = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);
        var paused = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);
        paused.Pause(Now.AddMinutes(1), PauseReason.User);

        Assert.Throws<InvalidOperationException>(() => running.Resume(Now));
        Assert.Throws<InvalidOperationException>(() => running.StartAwaiting(Now));
        Assert.Throws<InvalidOperationException>(() => paused.Pause(Now, PauseReason.User));
        Assert.Throws<InvalidOperationException>(() => paused.CompleteIfElapsed(Now.AddHours(1)));
    }

    [Fact]
    public void Completion_before_deadline_returns_null_and_keeps_running()
    {
        var session = PomodoroSession.Start(Guid.NewGuid(), PomodoroConfiguration.Standard, Now);

        var completed = session.CompleteIfElapsed(Now.AddMinutes(24).AddSeconds(59));

        Assert.Null(completed);
        Assert.Equal(PomodoroStatus.Running, session.Status);
    }

    private static void CompleteFocusAndBreak(PomodoroSession session, DateTimeOffset cycleStart)
    {
        if (session.Status == PomodoroStatus.Awaiting)
        {
            session.StartAwaiting(cycleStart);
        }

        session.CompleteIfElapsed(cycleStart.AddMinutes(25));
        session.StartAwaiting(cycleStart.AddMinutes(26));
        session.CompleteIfElapsed(cycleStart.AddMinutes(31));
    }
}
