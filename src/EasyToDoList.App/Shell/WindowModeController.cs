using System.Windows;

namespace EasyToDoList.App.Shell;

public sealed class WindowModeController(Window window)
{
    public void Apply(ShellDisplayMode mode)
    {
        if (mode == ShellDisplayMode.HiddenToTray)
        {
            window.Hide();
            return;
        }

        var isOverlay = mode == ShellDisplayMode.Overlay;
        var workArea = SystemParameters.WorkArea;
        var width = isOverlay ? 380 : Math.Min(1180, Math.Max(640, workArea.Width - 32));
        var height = isOverlay ? Math.Min(540, workArea.Height - 32) : Math.Min(760, Math.Max(480, workArea.Height - 32));
        window.Topmost = isOverlay;
        window.ResizeMode = isOverlay ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        window.MinWidth = isOverlay ? width : Math.Min(900, width);
        window.MinHeight = isOverlay ? height : Math.Min(600, height);
        window.Width = width;
        window.Height = height;
        window.Title = isOverlay ? "EasyToDoList — Overlay" : "EasyToDoList — Hội quán nhiệm vụ";

        if (!window.IsVisible)
        {
            window.Show();
        }
    }
}
