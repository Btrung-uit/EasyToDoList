using EasyToDoList.Core.Pomodoro;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public static class PomodoroSessionEndCheckpoint
{
    // SessionEnding cannot await the UI dispatcher; use an independent, synchronous transaction.
    public static void Save(string databasePath, DateTimeOffset nowUtc)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            DefaultTimeout = 2,
            Pooling = false,
        }.ToString();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
        using var transaction = db.Database.BeginTransaction();
        var session = db.PomodoroSessions.SingleOrDefault(session => session.Status == PomodoroStatus.Running);
        if (session is not null)
        {
            if (session.GetSnapshot(nowUtc).RemainingSeconds > 0)
                session.Pause(nowUtc, PauseReason.ApplicationExit);
            else
                // Preserve the last checkpoint if completion has not committed; never award on shutdown.
                session.RecoverAsPaused(nowUtc);
            db.SaveChanges();
        }
        transaction.Commit();
    }
}
