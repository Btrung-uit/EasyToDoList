using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace EasyToDoList.App.Shell;

public sealed class WindowModeController(Window window)
{
    private ShellDisplayMode? _lastMode;
    private Rect? _dashboardBounds;
    private WindowState _dashboardState = WindowState.Normal;
    internal bool IsApplying { get; private set; }

    public void Apply(ShellDisplayMode mode, double? overlayLeft = null, double? overlayTop = null)
    {
        if (mode == ShellDisplayMode.HiddenToTray)
        {
            window.Hide();
            return;
        }

        IsApplying = true;
        try
        {
            if (_lastMode == ShellDisplayMode.Dashboard)
            {
                if (mode == ShellDisplayMode.Dashboard)
                {
                    if (!window.IsVisible) window.Show();
                    return;
                }
                _dashboardBounds = window.RestoreBounds.IsEmpty ? null : window.RestoreBounds;
                _dashboardState = window.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
            }

            var isOverlay = mode == ShellDisplayMode.Overlay;
            window.SizeToContent = SizeToContent.Manual;
            window.WindowState = WindowState.Normal;
            if (!isOverlay && _dashboardBounds is { } remembered)
            {
                window.Left = remembered.Left;
                window.Top = remembered.Top;
            }
            var workArea = GetWorkArea(window);
            var defaultWidth = Math.Min(1180, Math.Max(640, workArea.Width - 32));
            var defaultHeight = Math.Min(760, Math.Max(480, workArea.Height - 32));
            var dashboard = _dashboardBounds ?? new Rect(
                workArea.Left + (workArea.Width - defaultWidth) / 2,
                workArea.Top + (workArea.Height - defaultHeight) / 2, defaultWidth, defaultHeight);
            var width = isOverlay ? Math.Min(320, workArea.Width) : Math.Min(dashboard.Width, workArea.Width);
            var height = isOverlay ? Math.Min(300, workArea.Height - 32) : Math.Min(dashboard.Height, workArea.Height);
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
            else
            {
                window.Left = Math.Clamp(dashboard.Left, workArea.Left, workArea.Right - width);
                window.Top = Math.Clamp(dashboard.Top, workArea.Top, workArea.Bottom - height);
                window.WindowState = _dashboardState;
            }

            _lastMode = mode;
            if (!window.IsVisible)
            {
                window.Show();
            }
        }
        finally { IsApplying = false; }
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
