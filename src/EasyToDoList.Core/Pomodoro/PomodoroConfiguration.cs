namespace EasyToDoList.Core.Pomodoro;

public sealed record PomodoroConfiguration
{
    public PomodoroConfiguration(
        int focusMinutes,
        int shortBreakMinutes,
        int longBreakMinutes,
        int longBreakInterval)
    {
        if (focusMinutes is < 15 or > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(focusMinutes),
                "Focus must be between 15 and 60 minutes.");
        }

        if (shortBreakMinutes * 100 < focusMinutes * 20
            || shortBreakMinutes * 100 > focusMinutes * 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shortBreakMinutes),
                "Short break must be between 20% and 30% of focus time.");
        }

        if (longBreakMinutes is < 15 or > 30 || longBreakMinutes <= shortBreakMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longBreakMinutes),
                "Long break must be 15–30 minutes and longer than the short break.");
        }

        if (longBreakInterval is not (3 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(longBreakInterval),
                "Long break interval must be three or four focus phases.");
        }

        FocusMinutes = focusMinutes;
        ShortBreakMinutes = shortBreakMinutes;
        LongBreakMinutes = longBreakMinutes;
        LongBreakInterval = longBreakInterval;
    }

    public static PomodoroConfiguration Starter { get; } = new(15, 3, 15, 4);

    public static PomodoroConfiguration Standard { get; } = new(25, 5, 20, 4);

    public static PomodoroConfiguration DeepFocus { get; } = new(50, 10, 25, 4);

    public int FocusMinutes { get; }

    public int ShortBreakMinutes { get; }

    public int LongBreakMinutes { get; }

    public int LongBreakInterval { get; }
}
