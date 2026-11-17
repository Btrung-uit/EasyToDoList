using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace EasyToDoList.App.Shell;

public sealed class WindowModeController(Window window)
{
    public void Apply(ShellDisplayMode mode, double? overlayLeft = null, double? overlayTop = null)
    {
        if (mode == ShellDisplayMode.HiddenToTray)
        {
            window.Hide();
            return;
        }

        var isOverlay = mode == ShellDisplayMode.Overlay;
        var workArea = GetWorkArea(window);
        var width = isOverlay ? Math.Min(320, workArea.Width) : Math.Min(1180, Math.Max(640, workArea.Width - 32));
        var height = isOverlay ? Math.Min(300, workArea.Height - 32) : Math.Min(760, Math.Max(480, workArea.Height - 32));
        window.SizeToContent = SizeToContent.Manual;
        window.Topmost = isOverlay;
        window.ResizeMode = isOverlay ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        window.MinWidth = isOverlay ? width : Math.Min(900, width);
        window.MinHeight = isOverlay ? Math.Min(160, height) : Math.Min(600, height);
        window.MaxHeight = isOverlay ? workArea.Height - 32 : double.PositiveInfinity;
        window.Width = width;
        window.Height = height;
        window.SizeToContent = isOverlay ? SizeToContent.Height : SizeToContent.Manual;
        window.Title = isOverlay ? "EasyToDoList — Overlay" : "EasyToDoList — Hội quán nhiệm vụ";

        if (isOverlay)
        {
            window.Left = Math.Clamp(
                overlayLeft ?? workArea.Right - width - 16,
                workArea.Left,
                workArea.Right - width);
            window.Top = Math.Clamp(
                overlayTop ?? workArea.Top + 16,
                workArea.Top,
                workArea.Bottom - height);
        }

        if (!window.IsVisible)
        {
            window.Show();
        }
    }

    internal static Rect GetWorkArea(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null) return SystemParameters.WorkArea;
        var bounds = Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
        var area = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        area.Transform(source.CompositionTarget.TransformFromDevice);
        return area;
    }
}
