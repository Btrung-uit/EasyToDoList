using EasyToDoList.Core.Pomodoro;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqlitePomodoroStore(AppDbContext dbContext) : IPomodoroStore
{
    public async Task AddAsync(
        PomodoroSession session,
        CancellationToken cancellationToken) =>
        await dbContext.PomodoroSessions.AddAsync(session, cancellationToken);

    public async Task<PomodoroSession?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var session = await dbContext.PomodoroSessions
            .Where(session => session.Status == PomodoroStatus.Running
                || session.Status == PomodoroStatus.Paused
                || session.Status == PomodoroStatus.Awaiting)
            .SingleOrDefaultAsync(cancellationToken);

        if (session is not null)
        {
            await dbContext.Entry(session).ReloadAsync(cancellationToken);
        }

        return session;
    }
}
