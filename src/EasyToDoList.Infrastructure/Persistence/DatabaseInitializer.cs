using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class DatabaseInitializer(string databasePath)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
        }.ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
