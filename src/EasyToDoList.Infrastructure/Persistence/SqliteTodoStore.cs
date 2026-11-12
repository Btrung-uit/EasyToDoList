using EasyToDoList.Core.Todos;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteTodoStore(AppDbContext dbContext) : ITodoStore
{
    public async Task AddAsync(TodoItem todo, CancellationToken cancellationToken) =>
        await dbContext.TodoItems.AddAsync(todo, cancellationToken);

    public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.TodoItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TodoItem>> GetTodayAsync(
        DateOnly localDate,
        CancellationToken cancellationToken) =>
        await dbContext.TodoItems
            .Where(item =>
                item.Status != TodoStatus.Completed
                && item.Status != TodoStatus.Cancelled
                && item.StartDate <= localDate)
            .OrderBy(item => item.Deadline)
            .ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);
}
