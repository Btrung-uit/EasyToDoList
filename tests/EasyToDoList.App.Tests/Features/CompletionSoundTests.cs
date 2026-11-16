using System.Text;
using EasyToDoList.App.Features.Today;

namespace EasyToDoList.App.Tests.Features;

public sealed class CompletionSoundTests
{
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
