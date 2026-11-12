namespace EasyToDoList.Core.Economy;

public readonly record struct TodoRewardInput(
    int BaseExperience,
    int DurationDays,
    int RemainingDays,
    bool IsOverdue);
