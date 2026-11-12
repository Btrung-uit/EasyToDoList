using EasyToDoList.Core.Todos;

namespace EasyToDoList.Core.Tests.Todos;

public sealed class TodoItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public void Create_trims_title_and_accepts_same_day_deadline()
    {
        var todo = TodoItem.Create(Guid.NewGuid(), "  Ôn cấu trúc dữ liệu  ", Today, Today, Now, Today);

        Assert.Equal("Ôn cấu trúc dữ liệu", todo.Title);
        Assert.Equal(TodoStatus.Active, todo.Status);
    }

    [Fact]
    public void Create_rejects_blank_title()
    {
        Assert.Throws<ArgumentException>(() =>
            TodoItem.Create(Guid.NewGuid(), "   ", Today, Today, Now, Today));
    }

    [Fact]
    public void Create_rejects_deadline_before_start()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TodoItem.Create(Guid.NewGuid(), "Task", Today, Today.AddDays(-1), Now, Today));
    }

    [Fact]
    public void Create_uses_schedule_relative_to_local_today()
    {
        var scheduled = TodoItem.Create(
            Guid.NewGuid(), "Future", Today.AddDays(1), Today.AddDays(2), Now, Today);
        var overdue = TodoItem.Create(
            Guid.NewGuid(), "Late", Today.AddDays(-2), Today.AddDays(-1), Now, Today);

        Assert.Equal(TodoStatus.Scheduled, scheduled.Status);
        Assert.Equal(TodoStatus.Overdue, overdue.Status);
    }

    [Fact]
    public void UpdateSchedule_changes_unlocked_reward_window()
    {
        var todo = CreateActiveTodo();

        todo.UpdateSchedule(Today.AddDays(1), Today.AddDays(3), Now.AddMinutes(1), Today);

        Assert.Equal(Today.AddDays(1), todo.StartDate);
        Assert.Equal(Today.AddDays(3), todo.Deadline);
        Assert.Equal(TodoStatus.Scheduled, todo.Status);
    }

    [Fact]
    public void UpdateSchedule_rejects_changes_after_reward_lock()
    {
        var todo = CreateActiveTodo();
        todo.LockRewardSnapshot(20, Now);

        Assert.Throws<InvalidOperationException>(() =>
            todo.UpdateSchedule(Today, Today.AddDays(2), Now.AddMinutes(1), Today));
    }

    [Fact]
    public void LockRewardSnapshot_is_idempotent_and_never_increases_reward()
    {
        var todo = CreateActiveTodo();

        todo.LockRewardSnapshot(20, Now);
        todo.LockRewardSnapshot(999, Now.AddMinutes(1));

        Assert.Equal(20, todo.BaseExperience);
        Assert.Equal(2, todo.RewardDurationDays);
        Assert.Equal(Today, todo.RewardStartDate);
        Assert.Equal(Today.AddDays(1), todo.RewardDeadline);
        Assert.Equal(Now, todo.RewardLockedAtUtc);
    }

    [Theory]
    [InlineData(TodoStatus.Scheduled)]
    [InlineData(TodoStatus.Active)]
    [InlineData(TodoStatus.Overdue)]
    public void Complete_and_undo_restore_previous_open_status(TodoStatus status)
    {
        var todo = CreateTodoInStatus(status);
        var transactionId = Guid.NewGuid();

        todo.Complete(Now.AddMinutes(1), transactionId);
        todo.UndoCompletion(Now.AddMinutes(2));

        Assert.Equal(status, todo.Status);
        Assert.Null(todo.CompletedAtUtc);
        Assert.Null(todo.CompletionTransactionId);
    }

    [Fact]
    public void Complete_rejects_terminal_state()
    {
        var todo = CreateActiveTodo();
        todo.Cancel(Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            todo.Complete(Now.AddMinutes(2), Guid.NewGuid()));
    }

    [Theory]
    [InlineData(TodoStatus.Scheduled)]
    [InlineData(TodoStatus.Active)]
    [InlineData(TodoStatus.Overdue)]
    public void Cancel_accepts_each_open_status(TodoStatus status)
    {
        var todo = CreateTodoInStatus(status);

        todo.Cancel(Now.AddMinutes(1));

        Assert.Equal(TodoStatus.Cancelled, todo.Status);
        Assert.Equal(Now.AddMinutes(1), todo.CancelledAtUtc);
    }

    [Fact]
    public void Undo_rejects_non_completed_state()
    {
        var todo = CreateActiveTodo();

        Assert.Throws<InvalidOperationException>(() => todo.UndoCompletion(Now));
    }

    private static TodoItem CreateActiveTodo() => TodoItem.Create(
        Guid.NewGuid(), "Task", Today, Today.AddDays(1), Now, Today);

    private static TodoItem CreateTodoInStatus(TodoStatus status) => status switch
    {
        TodoStatus.Scheduled => TodoItem.Create(
            Guid.NewGuid(), "Task", Today.AddDays(1), Today.AddDays(2), Now, Today),
        TodoStatus.Active => CreateActiveTodo(),
        TodoStatus.Overdue => TodoItem.Create(
            Guid.NewGuid(), "Task", Today.AddDays(-2), Today.AddDays(-1), Now, Today),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
