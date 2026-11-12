using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class DatabaseInitializer(string databasePath)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
