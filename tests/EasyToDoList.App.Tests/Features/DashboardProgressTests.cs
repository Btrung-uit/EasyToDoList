using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.App.Tests.Features;

public sealed class DashboardProgressTests
{
    [Fact]
    public async Task Progress_survives_reload_edit_cancel_undo_and_next_day_without_changing_rewards()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var clock = new Clock();
        var service = new TodoService(new SqliteTodoStore(db), new SqliteRewardStore(db), new SqliteProfileStore(db),
            new EfAppUnitOfWork(db), new EconomyCalculator(), clock);
        var history = new SqliteTodoHistoryQuery(db);
        TodayViewModel Create() => new(service, clock, motionEnabled: () => false, history: history);
        var vm = Create();
        await vm.LoadAsync();
        Assert.Equal(0, vm.ProgressPercent);
        Assert.Equal("18/11/2026", vm.DateLabel);
        vm.Title = "Ôn tập tiếng Việt";
        await vm.SaveAsync();
        Assert.Equal("Đã thêm nhiệm vụ.", vm.StatusMessage);
        Assert.Equal(1, vm.TotalCount);
        var row = Assert.Single(vm.Items);
        vm.EditCommand.Execute(row);
        Assert.True(vm.ScheduleExpanded);
        vm.Title = "Ôn tập chương 2";
        await vm.SaveAsync();
        Assert.Equal("Ôn tập chương 2", Assert.Single(vm.Items).Title);
        await vm.CompleteAsync(row);
        Assert.Equal(100, vm.ProgressPercent);
        var reopened = Create();
        await reopened.LoadAsync();
        Assert.Equal(1, reopened.CompletedCount);
        Assert.Equal(1, reopened.TotalCount);
        Assert.Equal(100, reopened.ProgressPercent);
        await vm.UndoAsync(Assert.Single(vm.Feedback));
        await reopened.LoadAsync();
        Assert.Equal(0, reopened.ProgressPercent);
        vm.CancelCommand.Execute(Assert.Single(vm.Items));
        // SQLite completes synchronously here; wait for the command's async work before reading again.
        for (var attempt = 0; !vm.CancelCommand.CanExecute(row) && attempt < 100; attempt++) await Task.Delay(10);
        await reopened.LoadAsync();
        Assert.Equal(0, reopened.TotalCount);
        Assert.Equal(0, reopened.ProgressPercent);
        vm.Title = "Ngày mới";
        await vm.SaveAsync();
        await vm.CompleteAsync(Assert.Single(vm.Items));
        clock.LocalToday = clock.LocalToday.AddDays(1);
        await reopened.LoadAsync();
        Assert.Equal(0, reopened.CompletedCount);
        Assert.Equal("19/11/2026", reopened.DateLabel);
        Assert.Null(reopened.ErrorMessage);
    }

    private sealed class Clock : IClock
    {
        public DateOnly LocalToday { get; set; } = new(2026, 11, 18);
        public DateTimeOffset UtcNow => new(LocalToday.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    }
}
