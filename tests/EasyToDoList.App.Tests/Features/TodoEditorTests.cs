using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.App.Tests.Features;

public sealed class TodoEditorTests
{
    [Theory]
    [InlineData(false, "1")]
    [InlineData(false, "Nhiệm vụ khác")]
    [InlineData(true, "1")]
    [InlineData(true, "Nhiệm vụ khác")]
    public async Task Closing_edited_todo_allows_new_save_without_restarting(bool cancel, string newTitle)
    {
        await using var db = await CreateDatabaseAsync();
        var (vm, service) = CreateEditor(db);
        vm.Title = "1";
        await vm.SaveAsync();
        var row = Assert.Single(vm.Items);
        vm.EditCommand.Execute(row);
        Assert.True(vm.IsEditing);
        if (cancel)
        {
            await service.CancelAsync(row.Id, CancellationToken.None);
            await vm.LoadAsync();
        }
        else await vm.CompleteAsync(row);

        var transactionsAfterClosing = await db.RewardTransactions.CountAsync();
        vm.Title = newTitle;
        await vm.SaveAsync();

        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsEditing);
        var created = Assert.Single(vm.Items, item => !item.IsCompleting);
        Assert.Equal(newTitle, created.Title);
        Assert.NotEqual(row.Id, created.Id);
        Assert.Equal(2, await db.TodoItems.CountAsync());
        Assert.Equal(transactionsAfterClosing, await db.RewardTransactions.CountAsync());
    }

    [Fact]
    public async Task Completing_another_todo_preserves_the_unsaved_edit_draft()
    {
        await using var db = await CreateDatabaseAsync();
        var (vm, _) = CreateEditor(db);
        vm.Title = "Đang sửa";
        await vm.SaveAsync();
        vm.Title = "Hoàn thành trước";
        await vm.SaveAsync();
        var edited = vm.Items.Single(row => row.Title == "Đang sửa");
        vm.EditCommand.Execute(edited);
        vm.Title = "Bản sửa chưa lưu";
        await vm.CompleteAsync(vm.Items.Single(row => row.Id != edited.Id));
        Assert.True(vm.IsEditing);
        Assert.Equal("Bản sửa chưa lưu", vm.Title);
        await vm.SaveAsync();
        Assert.Null(vm.ErrorMessage);
        Assert.Equal("Bản sửa chưa lưu", (await db.TodoItems.SingleAsync(item => item.Id == edited.Id)).Title);
        Assert.Equal(2, await db.TodoItems.CountAsync());
    }

    private static async Task<AppDbContext> CreateDatabaseAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static (TodayViewModel, TodoService) CreateEditor(AppDbContext db)
    {
        var clock = new Clock();
        var service = new TodoService(new SqliteTodoStore(db), new SqliteRewardStore(db), new SqliteProfileStore(db),
            new EfAppUnitOfWork(db), new EconomyCalculator(), clock);
        return (new TodayViewModel(service, clock), service);
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday => new(2026, 11, 17);
    }
}
