namespace EasyToDoList.Core.Pomodoro;

public interface IPomodoroStore
{
    Task AddAsync(PomodoroSession session, CancellationToken cancellationToken);

    Task<PomodoroSession?> GetActiveAsync(CancellationToken cancellationToken);
}
