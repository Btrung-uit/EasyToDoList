using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Tests.Features;

public sealed class CompletionFeedbackTests
{
    [Theory]
    [InlineData(0, "+38 EXP, +6 coin")]
    [InlineData(13, "+38 EXP (sớm +13), +6 coin")]
    public async Task Reward_message_only_shows_positive_early_bonus(int bonus, string expected)
    {
        var (vm, _, _) = await CreateAsync();
        await vm.CompleteAsync(vm.Items[0]);
        var feedback = Assert.Single(vm.Feedback);
        feedback = feedback with { Result = feedback.Result with { EarlyBonusExperience = bonus } };
        Assert.Equal(expected, feedback.CompactMessage);
        Assert.Equal($"One: {expected}", feedback.Message);
    }

    [Fact]
    public async Task Mascot_celebrates_at_ten_fps_once_then_returns_to_idle()
    {
        var (vm, _, clock) = await CreateAsync();
        await vm.CompleteAsync(vm.Items[0]);
        clock.Advance(350);
        Assert.EndsWith("Mascots/celebrate/celebrate-03.png", vm.MascotFrame);
        clock.Advance(300);
        Assert.EndsWith("Mascots/idle/idle-00.png", vm.MascotFrame);
    }

    [Fact]
    public async Task Pending_completion_disables_only_its_own_row()
    {
        var (vm, service, _) = await CreateAsync();
        var pending = new TaskCompletionSource<TodoCompletionResult>();
        service.Pending = pending;
        vm.CompleteCommand.Execute(vm.Items[0]);
        try
        {
            Assert.False(vm.CompleteCommand.CanExecute(vm.Items[0]));
            Assert.True(vm.CompleteCommand.CanExecute(vm.Items[1]));
        }
        finally { pending.SetException(new InvalidOperationException("test failure")); }
    }

    [Fact]
    public async Task Feedback_waits_for_commit_and_failure_keeps_row_without_reward()
    {
        var (vm, service, _) = await CreateAsync();
        service.Pending = new TaskCompletionSource<TodoCompletionResult>();
        vm.EditCommand.Execute(vm.Items[0]);
        var editedTitle = vm.Title;
        var task = vm.CompleteAsync(vm.Items[0]);
        Assert.Empty(vm.Feedback);
        Assert.False(vm.Items[0].IsCompleting);
        service.Pending.SetException(new InvalidOperationException("database unavailable"));
        await task;
        Assert.Empty(vm.Feedback);
        Assert.Equal(2, vm.Items.Count);
        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.IsEditing);
        Assert.Equal(editedTitle, vm.Title);
    }

    [Fact]
    public async Task Each_row_holds_its_slot_until_its_own_animation_finishes_even_after_reload()
    {
        var (vm, _, clock) = await CreateAsync();
        await vm.CompleteAsync(vm.Items[0]);
        Assert.True(vm.CompleteCommand.CanExecute(vm.Items[1]));
        clock.Advance(400);
        await vm.CompleteAsync(vm.Items[1]);
        await vm.LoadAsync();
        Assert.Equal(2, vm.Items.Count);
        clock.Advance(400);
        vm.RefreshFeedback();
        Assert.Single(vm.Items);
        clock.Advance(400);
        vm.RefreshFeedback();
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Combo_resets_window_and_expires_at_two_seconds_without_multiplying_reward()
    {
        var (vm, _, clock) = await CreateAsync();
        await vm.CompleteAsync(vm.Items[0]);
        clock.Advance(1900);
        await vm.CompleteAsync(vm.Items.Last());
        Assert.Equal(2, vm.ComboCount);
        Assert.All(vm.Feedback, x => Assert.Contains("38 EXP", x.Message));
        clock.Advance(1000);
        vm.RefreshFeedback();
        Assert.Equal(0.5, vm.ComboProgress, 3);
        clock.Advance(1000);
        vm.RefreshFeedback();
        Assert.Equal(0, vm.ComboCount);
    }

    [Fact]
    public async Task Undo_before_collapse_restores_row_and_adjusts_active_combo_once()
    {
        var (vm, _, _) = await CreateAsync();
        var row = vm.Items[0];
        await vm.CompleteAsync(row);
        var feedback = Assert.Single(vm.Feedback);
        await vm.UndoAsync(feedback);
        await vm.UndoAsync(feedback);
        Assert.Equal(2, vm.Items.Count);
        Assert.False(vm.Items[0].IsCompleting);
        Assert.Empty(vm.Feedback);
        Assert.Equal(0, vm.ComboCount);
        Assert.Equal(TodoStatus.Active, vm.Items[0].Item.Status);
    }

    [Fact]
    public async Task Failed_undo_remains_retryable_and_expired_undo_does_not_restore_row()
    {
        var (vm, service, clock) = await CreateAsync();
        await vm.CompleteAsync(vm.Items[0]);
        var feedback = Assert.Single(vm.Feedback);
        service.FailUndo = true;
        await vm.UndoAsync(feedback);
        Assert.Single(vm.Feedback);
        Assert.NotNull(vm.ErrorMessage);
        clock.Advance(5001);
        await vm.UndoAsync(feedback);
        vm.RefreshFeedback();
        Assert.Empty(vm.Feedback);
        Assert.Single(vm.Items);
    }

    [Fact]
    public async Task Reduced_motion_keeps_reward_and_undo_but_removes_row_without_burst()
    {
        var (vm, service, clock) = await CreateAsync();
        vm = new TodayViewModel(service, clock, motionEnabled: () => false);
        await vm.LoadAsync();
        await vm.CompleteAsync(vm.Items[0]);
        Assert.Single(vm.Items);
        Assert.Contains("38 EXP", Assert.Single(vm.Feedback).Message);
    }

    [Fact]
    public async Task Old_undo_does_not_reduce_a_new_combo_and_effect_failure_keeps_undo_available()
    {
        var (vm, _, clock) = await CreateAsync();
        vm.CompletionCommitted += (_, _) => throw new InvalidOperationException("audio unavailable");
        await vm.CompleteAsync(vm.Items[0]);
        var old = Assert.Single(vm.Feedback);
        clock.Advance(2100);
        vm.RefreshFeedback();
        await vm.CompleteAsync(vm.Items[0]);
        Assert.Equal(1, vm.ComboCount);
        await vm.UndoAsync(old);
        Assert.Equal(1, vm.ComboCount);
        Assert.Single(vm.Feedback);
    }

    [Fact]
    public async Task Second_completion_waits_for_database_without_losing_the_click()
    {
        var (vm, service, _) = await CreateAsync();
        var pending = new TaskCompletionSource<TodoCompletionResult>();
        service.Pending = pending;
        var firstRow = vm.Items[0];
        var first = vm.CompleteAsync(firstRow);
        service.Pending = null;
        var second = vm.CompleteAsync(vm.Items[1]);
        Assert.False(second.IsCompleted);
        Assert.Empty(vm.Feedback);
        pending.SetResult(await service.CompleteAsync(firstRow.Id, CancellationToken.None));
        await Task.WhenAll(first, second);
        Assert.Equal(2, vm.Feedback.Count);
        Assert.Equal(2, vm.ComboCount);
    }

    private static async Task<(TodayViewModel, Service, Clock)> CreateAsync()
    {
        var clock = new Clock();
        var service = new Service(clock);
        var vm = new TodayViewModel(service, clock);
        await vm.LoadAsync();
        return (vm, service, clock);
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday => DateOnly.FromDateTime(UtcNow.DateTime);
        public void Advance(int milliseconds) => UtcNow += TimeSpan.FromMilliseconds(milliseconds);
    }

    private sealed class Service(Clock clock) : ITodoService
    {
        private readonly List<TodoItem> _items = [Make(clock, "One"), Make(clock, "Two")];
        public TaskCompletionSource<TodoCompletionResult>? Pending { get; set; }
        public bool FailUndo { get; set; }
        public Task<TodoCompletionResult> CompleteAsync(Guid id, CancellationToken ct)
        {
            if (Pending is not null) return Pending.Task;
            var item = _items.Single(x => x.Id == id);
            var transaction = Guid.NewGuid();
            item.Complete(clock.UtcNow, transaction);
            return Task.FromResult(new TodoCompletionResult(item, new RewardAmount(38, 6), transaction, clock.UtcNow.AddSeconds(5)));
        }
        public Task UndoCompletionAsync(Guid id, Guid transaction, CancellationToken ct)
        {
            if (FailUndo) throw new InvalidOperationException("database unavailable");
            _items.Single(x => x.Id == id).UndoCompletion(clock.UtcNow);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<TodoItem>> GetTodayAsync(DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(_items.Where(x => x.Status == TodoStatus.Active).ToList());
        private static TodoItem Make(Clock clock, string title) => TodoItem.Create(Guid.NewGuid(), title, clock.LocalToday, clock.LocalToday, clock.UtcNow, clock.LocalToday);
        public Task<TodoItem> CreateAsync(CreateTodoCommand command, CancellationToken ct) => throw new NotSupportedException();
        public Task<TodoItem> UpdateAsync(UpdateTodoCommand command, CancellationToken ct) => throw new NotSupportedException();
        public Task<TodoItem> UpdateScheduleAsync(UpdateTodoScheduleCommand command, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
