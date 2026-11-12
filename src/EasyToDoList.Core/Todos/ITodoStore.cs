namespace EasyToDoList.Core.Todos;

public interface ITodoStore
{
    Task AddAsync(TodoItem todo, CancellationToken cancellationToken);

    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TodoItem>> GetTodayAsync(
        DateOnly localDate,
        CancellationToken cancellationToken);
}
