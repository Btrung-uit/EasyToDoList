using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Features.Today;

public sealed record CompletionFeedback(TodoCompletionResult Result, int OriginalIndex, int ComboGeneration)
{
    public string Message => $"{Result.Todo.Title}: +{Result.Reward.Experience} EXP (sớm +{Result.EarlyBonusExperience}), +{Result.Reward.Coins} coin";
    public string CompactMessage => $"+{Result.Reward.Experience} EXP (sớm +{Result.EarlyBonusExperience}), +{Result.Reward.Coins} coin";
}
