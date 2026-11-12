using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Profile;

namespace EasyToDoList.Core.Todos;

public sealed class TodoService(
    ITodoStore todoStore,
    IRewardStore rewardStore,
    IProfileStore profileStore,
    IAppUnitOfWork unitOfWork,
    EconomyCalculator economyCalculator,
    IClock clock) : ITodoService
{
    private static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(5);

    public Task<TodoItem> CreateAsync(
        CreateTodoCommand command,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var todo = TodoItem.Create(
                Guid.NewGuid(),
                command.Title,
                command.StartDate,
                command.Deadline,
                clock.UtcNow,
                clock.LocalToday);
            await todoStore.AddAsync(todo, ct);
            return todo;
        }, cancellationToken);

    public Task<TodoItem> UpdateScheduleAsync(
        UpdateTodoScheduleCommand command,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var todo = await GetRequiredAsync(command.TodoId, ct);
            todo.UpdateSchedule(
                command.StartDate,
                command.Deadline,
                clock.UtcNow,
                clock.LocalToday);
            return todo;
        }, cancellationToken);

    public Task<TodoCompletionResult> CompleteAsync(
        Guid todoId,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var todo = await GetRequiredAsync(todoId, ct);
            if (todo.Status == TodoStatus.Completed && todo.CompletionTransactionId is Guid existingId)
            {
                var existing = await rewardStore.GetAsync(existingId, ct)
                    ?? throw new InvalidOperationException("Completed Todo has no reward transaction.");
                return ToCompletionResult(todo, existing);
            }

            EnsureRewardSnapshot(todo);
            var rewardDeadline = todo.RewardDeadline
                ?? throw new InvalidOperationException("Todo reward deadline is not locked.");
            var baseExperience = todo.BaseExperience
                ?? throw new InvalidOperationException("Todo base experience is not locked.");
            var durationDays = todo.RewardDurationDays
                ?? throw new InvalidOperationException("Todo reward duration is not locked.");
            var isOverdue = todo.Status == TodoStatus.Overdue
                || clock.LocalToday > todo.Deadline;
            var earlyBonusDeadline = todo.Deadline < rewardDeadline ? todo.Deadline : rewardDeadline;
            var remainingDays = todo.ForfeitedEarlyBonus
                ? 0
                : earlyBonusDeadline.DayNumber - clock.LocalToday.DayNumber;
            var reward = economyCalculator.CalculateTodoCompletionReward(new TodoRewardInput(
                baseExperience,
                durationDays,
                remainingDays,
                isOverdue));
            var appliedReward = await profileStore.ApplyAsync(reward, ct);
            var transactionId = Guid.NewGuid();
            var transaction = RewardTransaction.Create(
                transactionId,
                RewardSourceType.Todo,
                todo.Id,
                isOverdue ? RewardReason.LateRecovery : RewardReason.Completion,
                appliedReward,
                $"todo:{todo.Id:N}:complete:v{todo.RowVersion}",
                clock.UtcNow);
            todo.Complete(clock.UtcNow, transactionId);
            await rewardStore.AddAsync(transaction, ct);
            return ToCompletionResult(todo, transaction);
        }, cancellationToken);

    public Task UndoCompletionAsync(
        Guid todoId,
        Guid completionTransactionId,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var idempotencyKey = $"todo:{todoId:N}:undo:{completionTransactionId:N}";
            if (await rewardStore.GetByIdempotencyKeyAsync(idempotencyKey, ct) is not null)
            {
                return true;
            }

            var todo = await GetRequiredAsync(todoId, ct);
            var completion = await rewardStore.GetAsync(completionTransactionId, ct)
                ?? throw new InvalidOperationException("Completion transaction does not exist.");
            if (completion.SourceType != RewardSourceType.Todo
                || completion.SourceId != todoId
                || todo.Status != TodoStatus.Completed
                || todo.CompletionTransactionId != completionTransactionId)
            {
                throw new InvalidOperationException("Completion transaction does not match this Todo.");
            }

            if (clock.UtcNow > completion.CreatedAtUtc + UndoWindow)
            {
                throw new InvalidOperationException("The five-second Undo window has expired.");
            }

            var requestedReversal = new RewardAmount(
                checked(-completion.ExperienceDelta),
                checked(-completion.CoinDelta));
            var appliedReversal = await profileStore.ApplyAsync(requestedReversal, ct);
            if (appliedReversal != requestedReversal)
            {
                throw new InvalidOperationException("Profile balance is too low to reverse this completion exactly.");
            }

            var reversal = RewardTransaction.Create(
                Guid.NewGuid(),
                RewardSourceType.Todo,
                todoId,
                RewardReason.Undo,
                appliedReversal,
                idempotencyKey,
                clock.UtcNow,
                completionTransactionId);
            todo.UndoCompletion(clock.UtcNow);
            await rewardStore.AddAsync(reversal, ct);
            return true;
        }, cancellationToken);

    public Task CancelAsync(Guid todoId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            var todo = await GetRequiredAsync(todoId, ct);
            if (todo.Status == TodoStatus.Cancelled)
            {
                return true;
            }

            EnsureRewardSnapshot(todo);
            var isOverdue = todo.Status == TodoStatus.Overdue || clock.LocalToday > todo.Deadline;
            var appliedPenalty = new RewardAmount(0, 0);
            if (!isOverdue)
            {
                var penalty = economyCalculator.CalculateCancellationPenalty(todo.BaseExperience!.Value);
                appliedPenalty = await profileStore.ApplyAsync(new RewardAmount(-penalty, 0), ct);
            }

            var transaction = RewardTransaction.Create(
                Guid.NewGuid(),
                RewardSourceType.Todo,
                todo.Id,
                RewardReason.Cancellation,
                appliedPenalty,
                $"todo:{todo.Id:N}:cancel:v{todo.RowVersion}",
                clock.UtcNow);
            todo.Cancel(clock.UtcNow);
            await rewardStore.AddAsync(transaction, ct);
            return true;
        }, cancellationToken);

    public Task<IReadOnlyList<TodoItem>> GetTodayAsync(
        DateOnly localDate,
        CancellationToken cancellationToken) =>
        todoStore.GetTodayAsync(localDate, cancellationToken);

    private async Task<TodoItem> GetRequiredAsync(Guid todoId, CancellationToken cancellationToken) =>
        await todoStore.GetAsync(todoId, cancellationToken)
        ?? throw new KeyNotFoundException($"Todo '{todoId}' does not exist.");

    private void EnsureRewardSnapshot(TodoItem todo)
    {
        if (todo.RewardLockedAtUtc is not null)
        {
            return;
        }

        var durationDays = todo.Deadline.DayNumber - todo.StartDate.DayNumber + 1;
        todo.LockRewardSnapshot(
            economyCalculator.CalculateTodoBaseExperience(durationDays),
            clock.UtcNow);
    }

    private static TodoCompletionResult ToCompletionResult(
        TodoItem todo,
        RewardTransaction transaction) =>
        new(
            todo,
            new RewardAmount(transaction.ExperienceDelta, transaction.CoinDelta),
            transaction.Id,
            transaction.CreatedAtUtc + UndoWindow);
}
