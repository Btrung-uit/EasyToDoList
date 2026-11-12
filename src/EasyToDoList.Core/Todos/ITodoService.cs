namespace EasyToDoList.Core.Todos;

public interface ITodoService
{
    Task<TodoItem> CreateAsync(CreateTodoCommand command, CancellationToken cancellationToken);

    Task<TodoItem> UpdateScheduleAsync(
        UpdateTodoScheduleCommand command,
        CancellationToken cancellationToken);

    Task<TodoCompletionResult> CompleteAsync(Guid todoId, CancellationToken cancellationToken);

    Task UndoCompletionAsync(
        Guid todoId,
        Guid completionTransactionId,
        CancellationToken cancellationToken);

    Task CancelAsync(Guid todoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TodoItem>> GetTodayAsync(
        DateOnly localDate,
        CancellationToken cancellationToken);
}
