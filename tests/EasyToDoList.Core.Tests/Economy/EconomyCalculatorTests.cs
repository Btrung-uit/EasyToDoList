using EasyToDoList.Core.Economy;

namespace EasyToDoList.Core.Tests.Economy;

public sealed class EconomyCalculatorTests
{
    private readonly EconomyCalculator _calculator = new();

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 25)]
    [InlineData(7, 50)]
    [InlineData(14, 85)]
    [InlineData(30, 85)]
    public void CalculateTodoBaseExperience_returns_capped_reward(int durationDays, int expected)
    {
        Assert.Equal(expected, _calculator.CalculateTodoBaseExperience(durationDays));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateTodoBaseExperience_rejects_non_positive_duration(int durationDays)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateTodoBaseExperience(durationDays));
    }

    [Theory]
    [InlineData(20, 1, 0, 0)]
    [InlineData(25, 2, 1, 13)]
    [InlineData(25, 2, 0, 0)]
    [InlineData(50, 7, 6, 25)]
    [InlineData(50, 7, 999, 25)]
    [InlineData(50, 7, -1, 0)]
    public void CalculateEarlyBonus_clamps_remaining_days_and_rounds_away_from_zero(
        int baseExperience,
        int durationDays,
        int remainingDays,
        int expected)
    {
        Assert.Equal(
            expected,
            _calculator.CalculateEarlyBonus(baseExperience, durationDays, remainingDays));
    }

    [Fact]
    public void CalculateEarlyBonus_rejects_non_positive_duration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateEarlyBonus(20, 0, 0));
    }

    [Theory]
    [InlineData(10, 2)]
    [InlineData(25, 3)]
    [InlineData(85, 9)]
    [InlineData(200, 10)]
    public void CalculateCancellationPenalty_rounds_and_clamps(int baseExperience, int expected)
    {
        Assert.Equal(expected, _calculator.CalculateCancellationPenalty(baseExperience));
    }

    [Theory]
    [InlineData(20, 6)]
    [InlineData(25, 8)]
    public void CalculateOverduePenalty_rounds_away_from_zero(int baseExperience, int expected)
    {
        Assert.Equal(expected, _calculator.CalculateOverduePenalty(baseExperience));
    }

    [Theory]
    [InlineData(50, 13)]
    [InlineData(85, 21)]
    public void CalculateLateRecovery_rounds_away_from_zero(int baseExperience, int expected)
    {
        Assert.Equal(expected, _calculator.CalculateLateRecovery(baseExperience));
    }

    [Theory]
    [InlineData(50, 7, 0, false, 50, 10)]
    [InlineData(25, 2, 1, false, 38, 6)]
    [InlineData(50, 7, 0, true, 13, 0)]
    public void CalculateTodoCompletionReward_returns_experience_and_coins(
        int baseExperience,
        int durationDays,
        int remainingDays,
        bool isOverdue,
        int expectedExperience,
        int expectedCoins)
    {
        var input = new TodoRewardInput(
            baseExperience,
            durationDays,
            remainingDays,
            isOverdue);

        var reward = _calculator.CalculateTodoCompletionReward(input);

        Assert.Equal(new RewardAmount(expectedExperience, expectedCoins), reward);
    }

    [Theory]
    [InlineData(15, 6, 3)]
    [InlineData(25, 10, 5)]
    [InlineData(40, 16, 8)]
    [InlineData(50, 20, 10)]
    [InlineData(60, 24, 12)]
    public void CalculatePomodoroReward_scales_per_five_minute_block(
        int focusMinutes,
        int expectedExperience,
        int expectedCoins)
    {
        Assert.Equal(
            new RewardAmount(expectedExperience, expectedCoins),
            _calculator.CalculatePomodoroReward(focusMinutes));
    }

    [Fact]
    public void CalculatePomodoroReward_rejects_negative_minutes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculatePomodoroReward(-1));
    }

    [Fact]
    public void BaseExperience_calculations_reject_negative_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateEarlyBonus(-1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateCancellationPenalty(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateOverduePenalty(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateLateRecovery(-1));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    public void CalculateTodoCompletionReward_rejects_non_positive_duration(
        int durationDays,
        bool isOverdue)
    {
        var input = new TodoRewardInput(20, durationDays, 0, isOverdue);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculator.CalculateTodoCompletionReward(input));
    }

    [Fact]
    public void CalculateTodoCompletionReward_rejects_experience_overflow()
    {
        var input = new TodoRewardInput(int.MaxValue, 2, 1, false);

        Assert.Throws<OverflowException>(
            () => _calculator.CalculateTodoCompletionReward(input));
    }
}
