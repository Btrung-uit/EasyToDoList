namespace EasyToDoList.Core.Pomodoro;

public interface IPomodoroService
{
    PomodoroSnapshot? Current { get; }

    Task StartAsync(PomodoroConfiguration configuration, CancellationToken cancellationToken);

    Task PauseAsync(PauseReason reason, CancellationToken cancellationToken);

    Task ResumeAsync(CancellationToken cancellationToken);

    Task SkipAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task TickAsync(CancellationToken cancellationToken);
}
