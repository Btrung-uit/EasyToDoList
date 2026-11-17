using System.IO;
using System.Media;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Features.Today;

internal sealed class CompletionSound : IDisposable
{
    private readonly SoundPlayer _player = new();
    private MemoryStream? _wave;

    public void Play(int combo, int volume)
        => PlayCue(combo <= 1 ? "completion" : $"combo-{Math.Clamp(combo, 2, 13):00}", volume);

    public void PlayCue(string? cue, int volume)
    {
        if (volume <= 0 || cue is null) return;
        try
        {
            _player.Stop();
            _wave?.Dispose();
            _wave = new MemoryStream(LoadWave(cue, volume));
            _player.Stream = _wave;
            _player.Load();
            _player.Play();
        }
        catch (Exception) { /* Audio failure must not interrupt a committed completion. */ }
    }

    internal static byte[] CreateWave(int combo, int volume)
        => LoadWave(combo <= 1 ? "completion" : $"combo-{Math.Clamp(combo, 2, 13):00}", volume);

    internal static byte[] LoadWave(string cue, int volume)
    {
        using var source = typeof(CompletionSound).Assembly.GetManifestResourceStream($"Audio.{cue}.wav")
            ?? throw new FileNotFoundException($"Missing audio cue: {cue}");
        using var stream = new MemoryStream();
        source.CopyTo(stream);
        var wave = stream.ToArray();
        for (var offset = 44; offset < wave.Length; offset += 2)
        {
            var sample = (short)(BitConverter.ToInt16(wave, offset) * Math.Clamp(volume, 0, 100) / 100);
            BitConverter.TryWriteBytes(wave.AsSpan(offset, 2), sample);
        }
        return wave;
    }

    internal static string? PomodoroCue(PomodoroSnapshot? previous, PomodoroSnapshot? current, bool bell, bool tick)
    {
        if (previous is null || current is null || previous.Id != current.Id) return null;
        if (bell && current.Status == PomodoroStatus.Awaiting && current.CompletedAtUtc is not null
            && previous.CompletedAtUtc != current.CompletedAtUtc) return "bell";
        if (tick && current.Status == PomodoroStatus.Running && current.RemainingSeconds is > 0 and <= 5
            && previous.RemainingSeconds != current.RemainingSeconds) return "tick";
        return null;
    }

    public void Dispose() { _player.Stop(); _player.Dispose(); _wave?.Dispose(); }
}
