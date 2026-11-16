using System.IO;
using System.Media;
using System.Text;

namespace EasyToDoList.App.Features.Today;

internal sealed class CompletionSound : IDisposable
{
    private readonly SoundPlayer _player = new();
    private MemoryStream? _wave;

    public void Play(int combo, int volume)
    {
        if (volume <= 0) return;
        try
        {
            _player.Stop();
            _wave?.Dispose();
            _wave = new MemoryStream(CreateWave(combo, volume));
            _player.Stream = _wave;
            _player.Load();
            _player.Play();
        }
        catch (Exception) { /* Audio failure must not interrupt a committed completion. */ }
    }

    internal static byte[] CreateWave(int combo, int volume)
    {
            using var wave = new MemoryStream();
            const int sampleRate = 22050;
            const int samples = sampleRate / 8;
            using (var writer = new BinaryWriter(wave, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                var frequency = 440 * Math.Pow(2, Math.Clamp(combo - 1, 0, 12) / 12d);
                for (var i = 0; i < samples; i++)
                {
                    var envelope = Math.Min(1, i / 100d) * (1 - i / (double)samples);
                    writer.Write((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * envelope * 8000 * Math.Clamp(volume, 0, 100) / 100));
                }
            }
            return wave.ToArray();
    }

    public void Dispose() { _player.Stop(); _player.Dispose(); _wave?.Dispose(); }
}
