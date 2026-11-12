namespace EasyToDoList.Core.Economy;

internal static class EconomyRules
{
    internal const int TodoBaseExperience = 20;
    internal const int TodoExperiencePerAdditionalDay = 5;
    internal const int TodoMaximumRewardDurationDays = 14;
    internal const decimal TodoMaximumEarlyBonusRatio = 0.5m;
    internal const decimal TodoCancellationPenaltyRatio = 0.1m;
    internal const int TodoMinimumCancellationPenalty = 2;
    internal const int TodoMaximumCancellationPenalty = 10;
    internal const decimal TodoOverduePenaltyRatio = 0.3m;
    internal const decimal TodoLateRecoveryRatio = 0.25m;
    internal const int TodoBaseExperiencePerCoin = 5;
    internal const int TodoEarlyBonusPerCoin = 10;
    internal const int PomodoroMinutesPerBlock = 5;
    internal const int PomodoroExperiencePerBlock = 2;
    internal const int PomodoroCoinsPerBlock = 1;
}
