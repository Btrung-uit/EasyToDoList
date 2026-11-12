namespace EasyToDoList.Core.Economy;

public interface IRewardStore
{
    Task AddAsync(RewardTransaction transaction, CancellationToken cancellationToken);

    Task<RewardTransaction?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<RewardTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
}
