using System.Globalization;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Profile;

namespace EasyToDoList.Core.Todos;

public sealed class DailyReconciliationService(
    ITodoStore todoStore,
    IRewardStore rewardStore,
    IProfileStore profileStore,
    IAppUnitOfWork unitOfWork,
    EconomyCalculator economyCalculator,
    IClock clock) : IDailyReconciliationService
{
    public async Task ReconcileAsync(DateOnly localDate, CancellationToken cancellationToken)
    {
        var candidateIds = await todoStore.GetReconciliationCandidateIdsAsync(
            localDate,
            cancellationToken);
        foreach (var todoId in candidateIds)
        {
            await unitOfWork.ExecuteAsync(async ct =>
            {
                var todo = await todoStore.GetAsync(todoId, ct);
                if (todo is null || todo.Status is TodoStatus.Completed or TodoStatus.Cancelled)
                {
                    return true;
                }

                if (todo.StartDate > localDate)
                {
                    return true;
                }

                if (todo.RewardLockedAtUtc is null)
                {
                    var durationDays = todo.Deadline.DayNumber - todo.StartDate.DayNumber + 1;
                    todo.LockRewardSnapshot(
                        economyCalculator.CalculateTodoBaseExperience(durationDays),
                        clock.UtcNow);
                }

                todo.ReconcileStatus(localDate, clock.UtcNow);
                if (todo.Status != TodoStatus.Overdue || todo.OverduePenaltyAppliedAtUtc is not null)
                {
                    return true;
                }

                var rewardDeadline = todo.RewardDeadline
                    ?? throw new InvalidOperationException("Todo reward deadline is not locked.");
                var idempotencyKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"todo-overdue:{todo.Id:N}:{rewardDeadline:yyyy-MM-dd}");
                var existing = await rewardStore.GetByIdempotencyKeyAsync(idempotencyKey, ct);
                if (existing is not null)
                {
                    todo.MarkOverduePenaltyApplied(existing.CreatedAtUtc);
                    return true;
                }

                var baseExperience = todo.BaseExperience
                    ?? throw new InvalidOperationException("Todo base experience is not locked.");
                var penalty = economyCalculator.CalculateOverduePenalty(baseExperience);
                var appliedPenalty = await profileStore.ApplyAsync(new RewardAmount(-penalty, 0), ct);
                var transaction = RewardTransaction.Create(
                    Guid.NewGuid(),
                    RewardSourceType.Todo,
                    todo.Id,
                    RewardReason.OverduePenalty,
                    appliedPenalty,
                    idempotencyKey,
                    clock.UtcNow);
                todo.MarkOverduePenaltyApplied(clock.UtcNow);
                await rewardStore.AddAsync(transaction, ct);
                return true;
            }, cancellationToken);
        }
    }
}
