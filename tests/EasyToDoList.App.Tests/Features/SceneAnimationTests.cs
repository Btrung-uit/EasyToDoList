using EasyToDoList.App.Presentation;

namespace EasyToDoList.App.Tests.Features;

public sealed class SceneAnimationTests
{
    [Theory]
    [InlineData("idle", 1.2, 1)]
    [InlineData("focus", 0.5, 3)]
    [InlineData("rest", 0.5, 2)]
    public void State_uses_contract_frame_rate(string state, double seconds, int expected) =>
        Assert.Equal((state, expected), SceneAnimation.Mascot(state, seconds, true, null));

    [Fact]
    public void Celebration_plays_once_then_returns_to_current_phase()
    {
        Assert.Equal(("celebrate", 3), SceneAnimation.Mascot("focus", 2.35, true, 2));
        Assert.Equal(("focus", 3), SceneAnimation.Mascot("focus", 2.6, true, 2));
        Assert.Equal(("rest", 0), SceneAnimation.Mascot("rest", 2.35, false, 2));
    }

    [Fact]
    public void Reduced_motion_pins_all_frames_and_translation()
    {
        Assert.Equal(("focus", 0), SceneAnimation.Mascot("focus", 100, false, null));
        Assert.Equal(0, SceneAnimation.Frame(100, 6, false));
        Assert.Equal(0, SceneAnimation.Frame(100, 4, false));
        Assert.Equal(0, SceneAnimation.MotionTime(100, false));
        Assert.Equal(3, SceneAnimation.Frame(0.5, 6, true));
        Assert.Equal(2, SceneAnimation.Frame(0.5, 4, true));
    }
}
