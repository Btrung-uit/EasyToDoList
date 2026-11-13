using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.Core.Tests.Pomodoro;

public sealed class PomodoroConfigurationTests
{
    [Fact]
    public void Presets_match_approved_durations()
    {
        Assert.Equal(new PomodoroConfiguration(15, 3, 15, 4), PomodoroConfiguration.Starter);
        Assert.Equal(new PomodoroConfiguration(25, 5, 20, 4), PomodoroConfiguration.Standard);
        Assert.Equal(new PomodoroConfiguration(50, 10, 25, 4), PomodoroConfiguration.DeepFocus);
    }

    [Theory]
    [InlineData(15, 3, 15, 3)]
    [InlineData(15, 4, 15, 4)]
    [InlineData(40, 8, 15, 4)]
    [InlineData(45, 10, 20, 4)]
    [InlineData(60, 12, 30, 3)]
    [InlineData(60, 18, 30, 4)]
    public void Custom_configuration_accepts_inclusive_boundaries(
        int focus,
        int shortBreak,
        int longBreak,
        int interval)
    {
        var configuration = new PomodoroConfiguration(focus, shortBreak, longBreak, interval);

        Assert.Equal(focus, configuration.FocusMinutes);
        Assert.Equal(shortBreak, configuration.ShortBreakMinutes);
        Assert.Equal(longBreak, configuration.LongBreakMinutes);
        Assert.Equal(interval, configuration.LongBreakInterval);
    }

    [Theory]
    [InlineData(40, 1, 15, 4)]
    [InlineData(60, 5, 15, 4)]
    [InlineData(10, 2, 15, 4)]
    [InlineData(61, 13, 20, 4)]
    [InlineData(40, 13, 20, 4)]
    [InlineData(60, 15, 14, 4)]
    [InlineData(60, 15, 31, 4)]
    [InlineData(60, 15, 15, 4)]
    [InlineData(25, 5, 20, 2)]
    [InlineData(25, 5, 20, 5)]
    public void Custom_configuration_rejects_unhealthy_combinations(
        int focus,
        int shortBreak,
        int longBreak,
        int interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PomodoroConfiguration(focus, shortBreak, longBreak, interval));
    }
}
