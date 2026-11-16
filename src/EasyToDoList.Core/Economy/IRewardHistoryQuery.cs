namespace EasyToDoList.Core.Economy;

public sealed record RewardHistoryEntry(
    Guid Id,
    RewardSourceType SourceType,
    Guid SourceId,
    RewardReason Reason,
    int ExperienceDelta,
    int CoinDelta,
    string IdempotencyKey,
    Guid? ReversesTransactionId,
    DateTimeOffset CreatedAtUtc);

public interface IRewardHistoryQuery
{
    Task<IReadOnlyList<RewardHistoryEntry>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
