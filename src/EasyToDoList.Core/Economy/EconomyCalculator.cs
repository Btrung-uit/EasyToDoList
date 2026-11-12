namespace EasyToDoList.Core.Economy;

public sealed class EconomyCalculator
{
    public int CalculateTodoBaseExperience(int durationDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationDays, 1);

        var rewardedDays = Math.Min(durationDays, EconomyRules.TodoMaximumRewardDurationDays);
        return EconomyRules.TodoBaseExperience
            + (EconomyRules.TodoExperiencePerAdditionalDay * (rewardedDays - 1));
    }

    public int CalculateEarlyBonus(int baseExperience, int durationDays, int remainingDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseExperience);
        ArgumentOutOfRangeException.ThrowIfLessThan(durationDays, 1);
        if (durationDays == 1)
        {
            return 0;
        }

        var clampedRemainingDays = Math.Clamp(remainingDays, 0, durationDays - 1);
        var bonus = baseExperience
            * EconomyRules.TodoMaximumEarlyBonusRatio
            * clampedRemainingDays
            / (durationDays - 1);
        return Round(bonus);
    }

    public int CalculateCancellationPenalty(int baseExperience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseExperience);

        var penalty = Round(baseExperience * EconomyRules.TodoCancellationPenaltyRatio);
        return Math.Clamp(
            penalty,
            EconomyRules.TodoMinimumCancellationPenalty,
            EconomyRules.TodoMaximumCancellationPenalty);
    }

    public int CalculateOverduePenalty(int baseExperience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseExperience);
        return Round(baseExperience * EconomyRules.TodoOverduePenaltyRatio);
    }

    public int CalculateLateRecovery(int baseExperience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseExperience);
        return Round(baseExperience * EconomyRules.TodoLateRecoveryRatio);
    }

    public RewardAmount CalculateTodoCompletionReward(TodoRewardInput input)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(input.DurationDays, 1);

        if (input.IsOverdue)
        {
            return new RewardAmount(CalculateLateRecovery(input.BaseExperience), 0);
        }

        var earlyBonus = CalculateEarlyBonus(
            input.BaseExperience,
            input.DurationDays,
            input.RemainingDays);
        var experience = checked(input.BaseExperience + earlyBonus);
        var coins = (input.BaseExperience / EconomyRules.TodoBaseExperiencePerCoin)
            + (earlyBonus / EconomyRules.TodoEarlyBonusPerCoin);
        return new RewardAmount(experience, coins);
    }

    public RewardAmount CalculatePomodoroReward(int focusMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(focusMinutes);

        var blocks = focusMinutes / EconomyRules.PomodoroMinutesPerBlock;
        return new RewardAmount(
            blocks * EconomyRules.PomodoroExperiencePerBlock,
            blocks * EconomyRules.PomodoroCoinsPerBlock);
    }

    private static int Round(decimal value) =>
        decimal.ToInt32(decimal.Round(value, 0, MidpointRounding.AwayFromZero));
}
