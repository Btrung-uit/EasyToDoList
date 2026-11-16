using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Profile;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteProfileQuery(AppDbContext dbContext) : IProfileQuery
{
    public async Task<ProfileSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var profile = await dbContext.PlayerProfiles
            .AsNoTracking()
            .SingleAsync(row => row.Id == 1, cancellationToken);
        return new ProfileSnapshot(
            checked((int)profile.TotalExperience),
            checked((int)profile.CoinBalance));
    }
}

public sealed class SqliteRewardHistoryQuery(AppDbContext dbContext) : IRewardHistoryQuery
{
    public async Task<IReadOnlyList<RewardHistoryEntry>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var entries = await dbContext.RewardTransactions
            .AsNoTracking()
            .Select(transaction => new RewardHistoryEntry(
                transaction.Id,
                transaction.SourceType,
                transaction.SourceId,
                transaction.Reason,
                transaction.ExperienceDelta,
                transaction.CoinDelta,
                transaction.IdempotencyKey,
                transaction.ReversesTransactionId,
                transaction.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        // shortcut: SQLite cannot order DateTimeOffset; add a sortable UTC column if the ledger becomes large.
        return entries
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToList();
    }
}
