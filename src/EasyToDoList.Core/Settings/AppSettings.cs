namespace EasyToDoList.Core.Settings;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default { get; } = new();

    public int MasterVolume { get; init; } = 80;

    public bool CompletionSoundEnabled { get; init; } = true;

    public bool ComboSoundEnabled { get; init; } = true;

    public bool TickSoundEnabled { get; init; } = true;

    public bool BellSoundEnabled { get; init; } = true;

    public bool AnimationsEnabled { get; init; } = true;

    public bool ReducedMotionEnabled { get; init; }

    public bool PomodoroNotificationsEnabled { get; init; } = true;

    public bool DueNotificationsEnabled { get; init; } = true;

    public bool OverdueNotificationsEnabled { get; init; } = true;

    public bool AutoStartEnabled { get; init; }

    public double? OverlayLeft { get; init; }

    public double? OverlayTop { get; init; }

    public void Validate()
    {
        if (MasterVolume is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MasterVolume));
        }

        if (OverlayLeft is double left && !double.IsFinite(left)
            || OverlayTop is double top && !double.IsFinite(top))
        {
            throw new ArgumentOutOfRangeException(nameof(OverlayLeft));
        }
    }
}
