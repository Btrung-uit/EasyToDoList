using EasyToDoList.Core.Todos;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteTodoStore(AppDbContext dbContext) : ITodoStore
{
    public async Task AddAsync(TodoItem todo, CancellationToken cancellationToken) =>
        await dbContext.TodoItems.AddAsync(todo, cancellationToken);

    public async Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var todo = await dbContext.TodoItems.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (todo is not null)
        {
            await dbContext.Entry(todo).ReloadAsync(cancellationToken);
        }

        return todo;
    }

    public async Task<IReadOnlyList<Guid>> GetReconciliationCandidateIdsAsync(
        DateOnly localDate,
        CancellationToken cancellationToken) =>
        await dbContext.TodoItems
            .AsNoTracking()
            .Where(item =>
                item.Status != TodoStatus.Completed
                && item.Status != TodoStatus.Cancelled
                && item.StartDate <= localDate)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TodoItem>> GetTodayAsync(
        DateOnly localDate,
        CancellationToken cancellationToken) =>
        await dbContext.TodoItems
            .AsNoTracking()
            .Where(item =>
                item.Status != TodoStatus.Completed
                && item.Status != TodoStatus.Cancelled
                && item.StartDate <= localDate)
            .OrderBy(item => item.Deadline)
            .ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);
}
