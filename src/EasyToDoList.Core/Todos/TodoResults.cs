using EasyToDoList.Core.Economy;

namespace EasyToDoList.Core.Todos;

public sealed record TodoCompletionResult(
    TodoItem Todo,
    RewardAmount Reward,
    Guid TransactionId,
    DateTimeOffset UndoUntilUtc);
