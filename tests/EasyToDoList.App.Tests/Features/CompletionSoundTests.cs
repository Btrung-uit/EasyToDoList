using System.Text;
using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Tests.Features;

public sealed class CompletionSoundTests
{
    [Theory]
    [InlineData("tick")]
    [InlineData("bell")]
    [InlineData("combo-13")]
    public void Shipped_cues_are_pcm_and_volume_zero_is_silent(string cue)
    {
        var wave = CompletionSound.LoadWave(cue, 100);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wave, 0, 4));
        Assert.Equal(22050, BitConverter.ToInt32(wave, 24));
        Assert.Equal(16, BitConverter.ToInt16(wave, 34));
        Assert.Equal(wave.Length - 44, BitConverter.ToInt32(wave, 40));
        Assert.Contains(wave.Skip(44), value => value != 0);
        Assert.All(CompletionSound.LoadWave(cue, 0).Skip(44), value => Assert.Equal(0, value));
    }

    [Fact]
    public void Pomodoro_cues_only_sound_on_new_end_or_last_five_seconds_and_respect_toggles()
    {
        var running = new PomodoroSnapshot(Guid.NewGuid(), PomodoroPhase.Focus, PomodoroStatus.Running,
            new PomodoroConfiguration(25, 5, 15, 4), 1500, 5, DateTimeOffset.UtcNow, null, null, null, 0, null, null);
        Assert.Equal("tick", CompletionSound.PomodoroCue(running with { RemainingSeconds = 6 }, running, true, true));
        Assert.Null(CompletionSound.PomodoroCue(running, running, true, true));
        Assert.Null(CompletionSound.PomodoroCue(running with { RemainingSeconds = 6 }, running, true, false));
        var ended = running with { Status = PomodoroStatus.Awaiting, CompletedAtUtc = DateTimeOffset.UtcNow };
        Assert.Equal("bell", CompletionSound.PomodoroCue(running, ended, true, true));
        Assert.Null(CompletionSound.PomodoroCue(ended, ended, true, true));
        Assert.Null(CompletionSound.PomodoroCue(null, ended, true, true));
        Assert.Null(CompletionSound.PomodoroCue(running, ended, false, true));
        Assert.Null(CompletionSound.PomodoroCue(running, running with { Status = PomodoroStatus.Paused }, true, true));
    }

    [Fact]
    public void Wave_is_valid_pcm_mutable_volume_and_higher_combo_has_higher_pitch()
    {
        var first = CompletionSound.CreateWave(1, 100);
        var higher = CompletionSound.CreateWave(6, 100);
        var muted = CompletionSound.CreateWave(1, 0);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(first, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(first, 8, 4));
        Assert.Equal(first.Length - 44, BitConverter.ToInt32(first, 40));
        Assert.Equal(22050, BitConverter.ToInt32(first, 24));
        Assert.All(muted.Skip(44), x => Assert.Equal(0, x));
        Assert.True(Crossings(higher) > Crossings(first));
        var quiet = CompletionSound.CreateWave(1, 25);
        Assert.True(Math.Abs(BitConverter.ToInt16(quiet, 244)) < Math.Abs(BitConverter.ToInt16(first, 244)));
    }

    private static int Crossings(byte[] wave)
    {
        var count = 0;
        for (var i = 46; i < wave.Length; i += 2)
            if (BitConverter.ToInt16(wave, i - 2) <= 0 && BitConverter.ToInt16(wave, i) > 0) count++;
        return count;
    }
}
