using EasyToDoList.Core.Economy;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteRewardStore(AppDbContext dbContext) : IRewardStore
{
    public async Task AddAsync(
        RewardTransaction transaction,
        CancellationToken cancellationToken) =>
        await dbContext.RewardTransactions.AddAsync(transaction, cancellationToken);

    public Task<RewardTransaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.RewardTransactions.SingleOrDefaultAsync(
            transaction => transaction.Id == id,
            cancellationToken);

    public Task<RewardTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.RewardTransactions.SingleOrDefaultAsync(
            transaction => transaction.IdempotencyKey == idempotencyKey,
            cancellationToken);
}
