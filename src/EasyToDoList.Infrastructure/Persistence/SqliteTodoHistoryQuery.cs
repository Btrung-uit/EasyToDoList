using EasyToDoList.Core.Todos;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteTodoHistoryQuery(
    AppDbContext dbContext,
    TimeZoneInfo? timeZone = null) : ITodoHistoryQuery
{
    private readonly TimeZoneInfo _timeZone = timeZone ?? TimeZoneInfo.Local;

    public async Task<IReadOnlyList<TodoHistoryEntry>> GetWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        var weekEnd = weekStart.AddDays(6);
        var todos = await dbContext.TodoItems
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var entries = new List<TodoHistoryEntry>();

        foreach (var todo in todos)
        {
            if (todo.Status == TodoStatus.Completed)
            {
                var completedDate = ToLocalDate(todo.CompletedAtUtc
                    ?? throw new InvalidOperationException("Completed Todo has no completion timestamp."));
                if (IsInWeek(completedDate, weekStart, weekEnd))
                {
                    entries.Add(CreateEntry(todo, completedDate, completedDate, null));
                }

                continue;
            }

            if (todo.Status == TodoStatus.Cancelled)
            {
                var cancelledDate = ToLocalDate(todo.CancelledAtUtc
                    ?? throw new InvalidOperationException("Cancelled Todo has no cancellation timestamp."));
                if (IsInWeek(cancelledDate, weekStart, weekEnd))
                {
                    entries.Add(CreateEntry(todo, cancelledDate, null, cancelledDate));
                }

                continue;
            }

            var firstDate = todo.StartDate < weekStart ? weekStart : todo.StartDate;
            var lastDate = todo.Deadline > weekEnd ? weekEnd : todo.Deadline;
            for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                entries.Add(CreateEntry(todo, date, null, null));
            }
        }

        return entries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Title)
            .ThenBy(entry => entry.TodoId)
            .ToList();
    }

    private DateOnly ToLocalDate(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, _timeZone).DateTime);

    private static bool IsInWeek(DateOnly date, DateOnly weekStart, DateOnly weekEnd) =>
        date >= weekStart && date <= weekEnd;

    private static TodoHistoryEntry CreateEntry(
        TodoItem todo,
        DateOnly date,
        DateOnly? completedDate,
        DateOnly? cancelledDate) =>
        new(
            todo.Id,
            todo.Title,
            todo.Status,
            date,
            todo.StartDate,
            todo.Deadline,
            completedDate,
            cancelledDate);
}
