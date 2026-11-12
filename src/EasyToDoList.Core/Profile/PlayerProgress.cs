namespace EasyToDoList.Core.Profile;

public sealed record PlayerProgress
{
    private PlayerProgress(
        int totalExperience,
        int level,
        int experienceIntoLevel,
        int experienceRequiredForNextLevel,
        AdventurerRank rank)
    {
        TotalExperience = totalExperience;
        Level = level;
        ExperienceIntoLevel = experienceIntoLevel;
        ExperienceRequiredForNextLevel = experienceRequiredForNextLevel;
        Rank = rank;
    }

    public int TotalExperience { get; }

    public int Level { get; }

    public int ExperienceIntoLevel { get; }

    public int ExperienceRequiredForNextLevel { get; }

    public int ExperienceRemainingToNextLevel =>
        ExperienceRequiredForNextLevel - ExperienceIntoLevel;

    public AdventurerRank Rank { get; }

    public static PlayerProgress FromExperience(int totalExperience)
    {
        var normalizedExperience = Math.Max(totalExperience, 0);
        var level = 1;
        var experienceIntoLevel = normalizedExperience;
        var experienceRequiredForNextLevel = ExperienceRequiredForNextLevelAt(level);

        while (experienceIntoLevel >= experienceRequiredForNextLevel)
        {
            experienceIntoLevel -= experienceRequiredForNextLevel;
            level++;
            experienceRequiredForNextLevel = ExperienceRequiredForNextLevelAt(level);
        }

        return new PlayerProgress(
            normalizedExperience,
            level,
            experienceIntoLevel,
            experienceRequiredForNextLevel,
            RankForLevel(level));
    }

    private static int ExperienceRequiredForNextLevelAt(int level) =>
        100 + (25 * (level - 1));

    private static AdventurerRank RankForLevel(int level) => level switch
    {
        >= 80 => AdventurerRank.S,
        >= 55 => AdventurerRank.A,
        >= 35 => AdventurerRank.B,
        >= 20 => AdventurerRank.C,
        >= 10 => AdventurerRank.D,
        >= 5 => AdventurerRank.E,
        _ => AdventurerRank.F,
    };
}
