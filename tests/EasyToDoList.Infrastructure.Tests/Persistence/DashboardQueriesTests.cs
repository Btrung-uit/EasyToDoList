using EasyToDoList.Core.Economy;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Tests.Persistence;

public sealed class DashboardQueriesTests
{
    [Fact]
    public async Task Profile_and_reward_queries_return_current_values_in_newest_first_order()
    {
        await using var fixture = await QueryFixture.CreateAsync();
        var profile = await fixture.Db.PlayerProfiles.SingleAsync();
        profile.TotalExperience = 125;
        profile.CoinBalance = 17;
        fixture.Db.RewardTransactions.AddRange(
            CreateReward("older", new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero)),
            CreateReward("newer", new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero)));
        await fixture.Db.SaveChangesAsync();

        var snapshot = await new SqliteProfileQuery(fixture.Db).GetAsync(CancellationToken.None);
        var history = await new SqliteRewardHistoryQuery(fixture.Db)
            .GetRecentAsync(1, CancellationToken.None);

        Assert.Equal(125, snapshot.TotalExperience);
        Assert.Equal(17, snapshot.CoinBalance);
        var entry = Assert.Single(history);
        Assert.Equal("newer", entry.IdempotencyKey);
    }

    private static RewardTransaction CreateReward(string key, DateTimeOffset createdAt) =>
        RewardTransaction.Create(
            Guid.NewGuid(),
            RewardSourceType.Todo,
            Guid.NewGuid(),
            RewardReason.Completion,
            new RewardAmount(20, 4),
            key,
            createdAt);

    private sealed class QueryFixture(SqliteConnection connection, AppDbContext db) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;

        public static async Task<QueryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();
            return new QueryFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
