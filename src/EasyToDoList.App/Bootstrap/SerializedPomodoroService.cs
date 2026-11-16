using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Bootstrap;

// UI commands and lifecycle callbacks share one EF context through this gate.
public sealed class SerializedPomodoroService(IPomodoroService inner) : IPomodoroService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public PomodoroSnapshot? Current => inner.Current;
    public Task StartAsync(PomodoroConfiguration configuration, CancellationToken ct) => RunAsync(() => inner.StartAsync(configuration, ct), ct);
    public Task PauseAsync(PauseReason reason, CancellationToken ct) => RunAsync(() => inner.PauseAsync(reason, ct), ct);
    public Task ResumeAsync(CancellationToken ct) => RunAsync(() => inner.ResumeAsync(ct), ct);
    public Task SkipAsync(CancellationToken ct) => RunAsync(() => inner.SkipAsync(ct), ct);
    public Task StopAsync(CancellationToken ct) => RunAsync(() => inner.StopAsync(ct), ct);
    public Task TickAsync(CancellationToken ct) => RunAsync(() => inner.TickAsync(ct), ct);
    public Task RecoverAsync(CancellationToken ct) => RunAsync(() => inner.RecoverAsync(ct), ct);
    private async Task RunAsync(Func<Task> operation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { await operation(); }
        finally { _gate.Release(); }
    }
}
