using EasyToDoList.Core.Pomodoro;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqlitePomodoroStore(AppDbContext dbContext) : IPomodoroStore
{
    public async Task AddAsync(
        PomodoroSession session,
        CancellationToken cancellationToken) =>
        await dbContext.PomodoroSessions.AddAsync(session, cancellationToken);

    public Task<PomodoroSession?> GetActiveAsync(CancellationToken cancellationToken) =>
        dbContext.PomodoroSessions
            .Where(session => session.Status == PomodoroStatus.Running
                || session.Status == PomodoroStatus.Paused
                || session.Status == PomodoroStatus.Awaiting)
            .SingleOrDefaultAsync(cancellationToken);
}
