using EasyToDoList.Core.Profile;

namespace EasyToDoList.Core.Tests.Profile;

public sealed class PlayerProgressTests
{
    [Theory]
    [InlineData(-1, 0, 1, 0, 100, AdventurerRank.F)]
    [InlineData(0, 0, 1, 0, 100, AdventurerRank.F)]
    [InlineData(99, 99, 1, 99, 100, AdventurerRank.F)]
    [InlineData(100, 100, 2, 0, 125, AdventurerRank.F)]
    public void FromExperience_returns_level_progress(
        int totalExperience,
        int expectedTotal,
        int expectedLevel,
        int expectedIntoLevel,
        int expectedForNextLevel,
        AdventurerRank expectedRank)
    {
        var progress = PlayerProgress.FromExperience(totalExperience);

        Assert.Equal(expectedTotal, progress.TotalExperience);
        Assert.Equal(expectedLevel, progress.Level);
        Assert.Equal(expectedIntoLevel, progress.ExperienceIntoLevel);
        Assert.Equal(expectedForNextLevel, progress.ExperienceRequiredForNextLevel);
        Assert.Equal(expectedForNextLevel - expectedIntoLevel, progress.ExperienceRemainingToNextLevel);
        Assert.Equal(expectedRank, progress.Rank);
    }

    [Theory]
    [InlineData(549, 4, AdventurerRank.F)]
    [InlineData(550, 5, AdventurerRank.E)]
    [InlineData(1799, 9, AdventurerRank.E)]
    [InlineData(1800, 10, AdventurerRank.D)]
    [InlineData(6174, 19, AdventurerRank.D)]
    [InlineData(6175, 20, AdventurerRank.C)]
    [InlineData(17424, 34, AdventurerRank.C)]
    [InlineData(17425, 35, AdventurerRank.B)]
    [InlineData(41174, 54, AdventurerRank.B)]
    [InlineData(41175, 55, AdventurerRank.A)]
    [InlineData(84924, 79, AdventurerRank.A)]
    [InlineData(84925, 80, AdventurerRank.S)]
    public void FromExperience_switches_rank_at_exact_level_boundaries(
        int totalExperience,
        int expectedLevel,
        AdventurerRank expectedRank)
    {
        var progress = PlayerProgress.FromExperience(totalExperience);

        Assert.Equal(expectedLevel, progress.Level);
        Assert.Equal(expectedRank, progress.Rank);
    }

    [Fact]
    public void FromExperience_reflects_level_and_rank_decrease()
    {
        var beforePenalty = PlayerProgress.FromExperience(84925);
        var afterPenalty = PlayerProgress.FromExperience(0);

        Assert.True(afterPenalty.Level < beforePenalty.Level);
        Assert.True(afterPenalty.Rank < beforePenalty.Rank);
    }
}
