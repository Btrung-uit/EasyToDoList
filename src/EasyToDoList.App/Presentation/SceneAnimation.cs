namespace EasyToDoList.App.Presentation;

internal static class SceneAnimation
{
    internal static double MotionTime(double seconds, bool enabled) => enabled ? seconds : 0;

    internal static int Frame(double seconds, int fps, bool enabled) =>
        (int)(MotionTime(seconds, enabled) * fps) % 6;

    internal static (string State, int Frame) Mascot(string state, double seconds, bool enabled, double? celebration)
    {
        if (enabled && celebration is { } start && seconds - start is >= 0 and < 0.6)
            return ("celebrate", Math.Min(5, (int)((seconds - start) * 10)));
        return (state, Frame(seconds, state == "rest" ? 5 : 6, enabled));
    }
}
