namespace EasyToDoList.Core.Todos;

public sealed record TodoHistoryEntry(
    Guid TodoId,
    string Title,
    TodoStatus Status,
    DateOnly Date,
    DateOnly PlannedStartDate,
    DateOnly PlannedDeadline,
    DateOnly? CompletedDate,
    DateOnly? CancelledDate);

public interface ITodoHistoryQuery
{
    Task<IReadOnlyList<TodoHistoryEntry>> GetWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken);
}
