namespace EasyToDoList.Core.Todos;

public sealed class TodoItem
{
    private TodoItem()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly Deadline { get; private set; }

    public TodoStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public DateOnly? RewardStartDate { get; private set; }

    public DateOnly? RewardDeadline { get; private set; }

    public int? RewardDurationDays { get; private set; }

    public int? BaseExperience { get; private set; }

    public DateTimeOffset? RewardLockedAtUtc { get; private set; }

    public bool ForfeitedEarlyBonus { get; private set; }

    public DateTimeOffset? OverduePenaltyAppliedAtUtc { get; private set; }

    public TodoStatus? StatusBeforeCompletion { get; private set; }

    public Guid? CompletionTransactionId { get; private set; }

    public long RowVersion { get; private set; }

    public static TodoItem Create(
        Guid id,
        string title,
        DateOnly startDate,
        DateOnly deadline,
        DateTimeOffset nowUtc,
        DateOnly localToday)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Todo ID cannot be empty.", nameof(id));
        }

        var normalizedTitle = NormalizeTitle(title);
        ValidateSchedule(startDate, deadline);

        return new TodoItem
        {
            Id = id,
            Title = normalizedTitle,
            StartDate = startDate,
            Deadline = deadline,
            Status = StatusFor(startDate, deadline, localToday),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            RowVersion = 1,
        };
    }

    public void UpdateSchedule(
        DateOnly startDate,
        DateOnly deadline,
        DateTimeOffset nowUtc,
        DateOnly localToday)
    {
        EnsureOpen();
        if (RewardLockedAtUtc is not null)
        {
            throw new InvalidOperationException("A locked reward schedule cannot be changed.");
        }

        ValidateSchedule(startDate, deadline);
        StartDate = startDate;
        Deadline = deadline;
        Status = StatusFor(startDate, deadline, localToday);
        Touch(nowUtc);
    }

    public void LockRewardSnapshot(int baseExperience, DateTimeOffset nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseExperience);
        if (RewardLockedAtUtc is not null)
        {
            return;
        }

        RewardStartDate = StartDate;
        RewardDeadline = Deadline;
        RewardDurationDays = Deadline.DayNumber - StartDate.DayNumber + 1;
        BaseExperience = baseExperience;
        RewardLockedAtUtc = nowUtc;
        Touch(nowUtc);
    }

    public void Complete(DateTimeOffset nowUtc, Guid transactionId)
    {
        EnsureOpen();
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Transaction ID cannot be empty.", nameof(transactionId));
        }

        StatusBeforeCompletion = Status;
        Status = TodoStatus.Completed;
        CompletedAtUtc = nowUtc;
        CompletionTransactionId = transactionId;
        Touch(nowUtc);
    }

    public void UndoCompletion(DateTimeOffset nowUtc)
    {
        if (Status != TodoStatus.Completed || StatusBeforeCompletion is null)
        {
            throw new InvalidOperationException("Only a completed Todo can be undone.");
        }

        Status = StatusBeforeCompletion.Value;
        StatusBeforeCompletion = null;
        CompletedAtUtc = null;
        CompletionTransactionId = null;
        Touch(nowUtc);
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        EnsureOpen();
        Status = TodoStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        Touch(nowUtc);
    }

    private static string NormalizeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var normalized = title.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Todo title cannot be blank.", nameof(title));
        }

        return normalized;
    }

    private static void ValidateSchedule(DateOnly startDate, DateOnly deadline)
    {
        if (deadline < startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(deadline), "Deadline cannot precede start date.");
        }
    }

    private static TodoStatus StatusFor(DateOnly startDate, DateOnly deadline, DateOnly localToday)
    {
        if (localToday < startDate)
        {
            return TodoStatus.Scheduled;
        }

        return localToday > deadline ? TodoStatus.Overdue : TodoStatus.Active;
    }

    private void EnsureOpen()
    {
        if (Status is TodoStatus.Completed or TodoStatus.Cancelled)
        {
            throw new InvalidOperationException($"Todo in {Status} state cannot be changed.");
        }
    }

    private void Touch(DateTimeOffset nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        RowVersion = checked(RowVersion + 1);
    }
}
