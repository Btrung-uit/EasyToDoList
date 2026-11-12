using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Tests.Todos;

public sealed class TodoHistoryQueryTests
{
    private static readonly DateOnly WeekStart = new(2026, 11, 9);
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 8, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo UtcPlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "Test-UTC+02",
        TimeSpan.FromHours(2),
        "Test UTC+02",
        "Test UTC+02");

    [Fact]
    public async Task GetWeek_places_open_todos_on_planned_days_and_terminal_todos_on_event_day()
    {
        await using var fixture = await HistoryFixture.CreateAsync();
        var scheduled = TodoItem.Create(
            Guid.NewGuid(), "Scheduled", WeekStart.AddDays(5), WeekStart.AddDays(5), CreatedAt, WeekStart);
        var active = TodoItem.Create(
            Guid.NewGuid(), "Active", WeekStart, WeekStart.AddDays(1), CreatedAt, WeekStart);
        var overdue = TodoItem.Create(
            Guid.NewGuid(), "Overdue", WeekStart.AddDays(-2), WeekStart, CreatedAt, WeekStart.AddDays(1));
        var completed = TodoItem.Create(
            Guid.NewGuid(), "Completed", WeekStart.AddDays(-3), WeekStart.AddDays(-2), CreatedAt, WeekStart.AddDays(-3));
        completed.Complete(
            new DateTimeOffset(2026, 11, 11, 22, 30, 0, TimeSpan.Zero),
            Guid.NewGuid());
        var cancelled = TodoItem.Create(
            Guid.NewGuid(), "Cancelled", WeekStart.AddDays(10), WeekStart.AddDays(10), CreatedAt, WeekStart);
        cancelled.Cancel(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
        fixture.Db.TodoItems.AddRange(scheduled, active, overdue, completed, cancelled);
        await fixture.Db.SaveChangesAsync();

        var result = await new SqliteTodoHistoryQuery(fixture.Db, UtcPlusTwo)
            .GetWeekAsync(WeekStart, CancellationToken.None);

        Assert.Equal(6, result.Count);
        Assert.Equal(2, result.Count(entry => entry.Status == TodoStatus.Active));
        Assert.Contains(result, entry => entry.Status == TodoStatus.Scheduled && entry.Date == WeekStart.AddDays(5));
        Assert.Contains(result, entry => entry.Status == TodoStatus.Overdue && entry.Date == WeekStart);
        var completedEntry = Assert.Single(result, entry => entry.Status == TodoStatus.Completed);
        Assert.Equal(WeekStart.AddDays(3), completedEntry.Date);
        Assert.Equal(WeekStart.AddDays(-3), completedEntry.PlannedStartDate);
        Assert.Equal(WeekStart.AddDays(-2), completedEntry.PlannedDeadline);
        Assert.Equal(WeekStart.AddDays(3), completedEntry.CompletedDate);
        var cancelledEntry = Assert.Single(result, entry => entry.Status == TodoStatus.Cancelled);
        Assert.Equal(WeekStart.AddDays(4), cancelledEntry.Date);
        Assert.Equal(WeekStart.AddDays(10), cancelledEntry.PlannedStartDate);
        Assert.Equal(WeekStart.AddDays(4), cancelledEntry.CancelledDate);
    }

    [Fact]
    public async Task GetWeek_clamps_open_projection_to_exact_seven_day_window()
    {
        await using var fixture = await HistoryFixture.CreateAsync();
        var todo = TodoItem.Create(
            Guid.NewGuid(),
            "Spanning",
            WeekStart.AddDays(-1),
            WeekStart.AddDays(7),
            CreatedAt,
            WeekStart);
        fixture.Db.TodoItems.Add(todo);
        await fixture.Db.SaveChangesAsync();

        var result = await new SqliteTodoHistoryQuery(fixture.Db, TimeZoneInfo.Utc)
            .GetWeekAsync(WeekStart, CancellationToken.None);

        Assert.Equal(7, result.Count);
        Assert.Equal(
            Enumerable.Range(0, 7).Select(WeekStart.AddDays),
            result.Select(entry => entry.Date));
    }

    [Fact]
    public async Task GetWeek_excludes_terminal_todo_when_event_date_is_outside_week()
    {
        await using var fixture = await HistoryFixture.CreateAsync();
        var todo = TodoItem.Create(
            Guid.NewGuid(), "Completed later", WeekStart, WeekStart.AddDays(1), CreatedAt, WeekStart);
        todo.Complete(
            new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero),
            Guid.NewGuid());
        fixture.Db.TodoItems.Add(todo);
        await fixture.Db.SaveChangesAsync();

        var result = await new SqliteTodoHistoryQuery(fixture.Db, TimeZoneInfo.Utc)
            .GetWeekAsync(WeekStart, CancellationToken.None);

        Assert.Empty(result);
    }

    private sealed class HistoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private HistoryFixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public AppDbContext Db { get; }

        public static async Task<HistoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();
            return new HistoryFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
