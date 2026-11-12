namespace EasyToDoList.Core.Economy;

public enum RewardSourceType
{
    Todo,
    Pomodoro,
    System,
}

public enum RewardReason
{
    Completion,
    EarlyBonus,
    OverduePenalty,
    LateRecovery,
    Cancellation,
    Undo,
}

public sealed class RewardTransaction
{
    private RewardTransaction()
    {
    }

    public Guid Id { get; private set; }

    public RewardSourceType SourceType { get; private set; }

    public Guid SourceId { get; private set; }

    public RewardReason Reason { get; private set; }

    public int ExperienceDelta { get; private set; }

    public int CoinDelta { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public Guid? ReversesTransactionId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static RewardTransaction Create(
        Guid id,
        RewardSourceType sourceType,
        Guid sourceId,
        RewardReason reason,
        RewardAmount amount,
        string idempotencyKey,
        DateTimeOffset createdAtUtc,
        Guid? reversesTransactionId = null)
    {
        if (id == Guid.Empty || sourceId == Guid.Empty)
        {
            throw new ArgumentException("Transaction and source IDs cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key cannot be blank.", nameof(idempotencyKey));
        }

        return new RewardTransaction
        {
            Id = id,
            SourceType = sourceType,
            SourceId = sourceId,
            Reason = reason,
            ExperienceDelta = amount.Experience,
            CoinDelta = amount.Coins,
            IdempotencyKey = idempotencyKey,
            ReversesTransactionId = reversesTransactionId,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
