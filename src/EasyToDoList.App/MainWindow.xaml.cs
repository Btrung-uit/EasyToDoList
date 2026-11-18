using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using EasyToDoList.App.Shell;
using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App;

public partial class MainWindow : Window
{
    public static readonly DependencyProperty IsCompactDashboardProperty = DependencyProperty.Register(
        nameof(IsCompactDashboard), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public bool IsCompactDashboard { get => (bool)GetValue(IsCompactDashboardProperty); private set => SetValue(IsCompactDashboardProperty, value); }
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeController _modeController;
    private readonly DispatcherTimer _overlayTimer;
    private readonly DispatcherTimer _feedbackTimer;
    private readonly CompletionSound _completionSound = new();
    private readonly CompletionSound _pomodoroSound = new();
    private PomodoroSnapshot? _lastAudioSnapshot;
    private bool _exitRequested;
    private IntPtr _windowHandle;
    private HwndSource? _windowSource;
    private byte? _windowAlpha;
    private bool _keyboardInteraction;
    private bool _nonClientMouseOver;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        OverlayFeedbackPopup.CustomPopupPlacementCallback = PlaceFeedbackPopup;
        _viewModel = viewModel;
        _lastAudioSnapshot = viewModel.PomodoroOverlay?.Snapshot;
        _modeController = new WindowModeController(this);
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _overlayTimer.Tick += OnOverlayTimerTick;
        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _feedbackTimer.Tick += OnFeedbackTimerTick;
        if (viewModel.Dashboard is not null) viewModel.Dashboard.Today.CompletionCommitted += OnCompletionCommitted;
        DataContext = viewModel;
        if (viewModel.Dashboard is { } dashboard) dashboard.Settings.PropertyChanged += OnSettingsChanged;
        if (viewModel.PomodoroOverlay is { } overlay) overlay.PropertyChanged += OnPomodoroChanged;
        UpdateScene();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.ExitRequested += OnExitRequested;
        if (viewModel.PomodoroOverlay is not null)
        {
            viewModel.PomodoroOverlay.StopConfirmationRequested += OnStopConfirmationRequested;
        }
        LocationChanged += OnLocationChanged;
        SizeChanged += OnWindowSizeChanged;
        Closed += OnClosed;
        _modeController.Apply(
            viewModel.DisplayMode,
            viewModel.Dashboard?.Settings.OverlayLeft,
            viewModel.Dashboard?.Settings.OverlayTop);
        _overlayTimer.Start();
        _feedbackTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(_windowHandle);
        _windowSource.AddHook(OnWindowMessage);
        SetWindowLong(_windowHandle, -20, GetWindowLong(_windowHandle, -20) | 0x80000);
        UpdateTransparency();
    }

    private IntPtr OnWindowMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x7C && wParam.ToInt32() == -20) // WM_STYLECHANGING, GWL_EXSTYLE
        {
            // HwndTarget otherwise strips WS_EX_LAYERED from windows with native title bars.
            Marshal.WriteInt32(lParam, 4, Marshal.ReadInt32(lParam, 4) | 0x80000);
            handled = true;
        }
        else if (message == 0xA0 && !_nonClientMouseOver) // WM_NCMOUSEMOVE
        {
            var tracking = new MouseTracking { Size = (uint)Marshal.SizeOf<MouseTracking>(), Flags = 0x12, Handle = handle };
            if (!TrackMouseEvent(ref tracking)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _nonClientMouseOver = true;
            UpdateTransparency();
        }
        else if (message == 0x2A2 || message is 0xA1 or 0xA4) // non-client leave / mouse down
        {
            if (message == 0x2A2) _nonClientMouseOver = false;
            else _keyboardInteraction = false;
            UpdateTransparency();
        }
        return IntPtr.Zero;
    }

    protected override void OnActivated(EventArgs e) { base.OnActivated(e); UpdateTransparency(); }
    protected override void OnDeactivated(EventArgs e) { base.OnDeactivated(e); UpdateTransparency(); }

    private void OnWindowInteractionChanged(object sender, MouseEventArgs e) => UpdateTransparency();
    private void OnFocusChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateTransparency();
    private void OnKeyboardInteraction(object sender, KeyEventArgs e) { _keyboardInteraction = true; UpdateTransparency(); }
    private void OnMouseInteraction(object sender, MouseButtonEventArgs e) { _keyboardInteraction = false; UpdateTransparency(); }

    private void UpdateTransparency()
    {
        if (_windowHandle == IntPtr.Zero || _viewModel is null) return;
        var feedback = OverlayFeedbackPopup.Child;
        var interacting = IsMouseOver || _nonClientMouseOver || feedback.IsMouseOver
            || (_keyboardInteraction && IsActive && (IsKeyboardFocusWithin || feedback.IsKeyboardFocusWithin));
        var alpha = (byte)(_viewModel.DisplayMode == ShellDisplayMode.Overlay && !interacting ? 128 : 255);
        if (_windowAlpha == alpha) return;
        // Native alpha makes the desktop visible through the HWND, including its title bar.
        if (!SetLayeredWindowAttributes(_windowHandle, 0, alpha, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
        _windowAlpha = alpha;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr handle, int index, int value);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr handle, uint color, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool TrackMouseEvent(ref MouseTracking tracking);
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseTracking { public uint Size; public uint Flags; public IntPtr Handle; public uint HoverTime; }

    private void OnFeedbackTimerTick(object? sender, EventArgs e) => _viewModel.Dashboard?.Today.RefreshFeedback();

    private void OnCompletionCommitted(object? sender, EventArgs e)
    {
        MascotScene.Celebrate();
        if (_viewModel.Dashboard is { } dashboard
            && (dashboard.Today.ComboCount >= 2
                ? dashboard.Settings.ComboSoundEnabled
                : dashboard.Settings.CompletionSoundEnabled))
            _completionSound.Play(dashboard.Today.ComboCount, dashboard.Settings.MasterVolume);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => UpdateScene();

    private void OnPomodoroChanged(object? sender, PropertyChangedEventArgs e) => UpdateScene();

    private void UpdateScene()
    {
        var settings = _viewModel.Dashboard?.Settings;
        var motion = settings is not null && settings.AnimationsEnabled && !settings.ReducedMotionEnabled;
        AmbientScene.MotionEnabled = motion && _viewModel.DisplayMode == ShellDisplayMode.Dashboard;
        MascotScene.MotionEnabled = motion;
        MascotScene.MascotState = _viewModel.PomodoroOverlay?.IsRunning == true
            ? (_viewModel.PomodoroOverlay.IsFocus ? "focus" : "rest") : "idle";
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.DisplayMode))
        {
            _nonClientMouseOver = false;
            UpdateScene();
            _modeController.Apply(
                _viewModel.DisplayMode,
                _viewModel.Dashboard?.Settings.OverlayLeft,
                _viewModel.Dashboard?.Settings.OverlayTop);
            UpdateTransparency();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        _viewModel.HideToTray();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _exitRequested = true;
        Close();
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        RepositionFeedbackPopup();
        if (_viewModel.DisplayMode == ShellDisplayMode.Overlay && !_modeController.IsApplying)
        {
            _viewModel.UpdateOverlayPosition(Left, Top);
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        IsCompactDashboard = ActualWidth < 1080;
        NavigationColumn.Width = new GridLength(IsCompactDashboard ? 64 : 176);
        NavigationHeading.Visibility = IsCompactDashboard ? Visibility.Collapsed : Visibility.Visible;
        RepositionFeedbackPopup();
        if (_viewModel.DisplayMode != ShellDisplayMode.Overlay || _modeController.IsApplying) return;
        var area = WindowModeController.GetWorkArea(this);
        MaxHeight = Math.Max(MinHeight, area.Height - 32);
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
    }

    private void RepositionFeedbackPopup()
    {
        if (!OverlayFeedbackPopup.IsOpen) return;
        // Popup has its own HWND and does not follow owner movement automatically.
        OverlayFeedbackPopup.HorizontalOffset += 1;
        OverlayFeedbackPopup.HorizontalOffset -= 1;
    }

    private CustomPopupPlacement[] PlaceFeedbackPopup(Size popupSize, Size targetSize, Point offset)
    {
        // Window size includes the title bar; Popup coordinates start inside the client area.
        var chromeHeight = ActualHeight - ((FrameworkElement)Content).ActualHeight;
        var chromeWidth = ActualWidth - ((FrameworkElement)Content).ActualWidth;
        var left = targetSize.Width - popupSize.Width;
        return
        [
            new(new Point(left, targetSize.Height - chromeHeight + 8), PopupPrimaryAxis.Horizontal),
            new(new Point(left, -popupSize.Height - chromeHeight - 8), PopupPrimaryAxis.Horizontal),
            new(new Point(-popupSize.Width - chromeWidth - 8, 0), PopupPrimaryAxis.Vertical),
            new(new Point(targetSize.Width - chromeWidth + 8, 0), PopupPrimaryAxis.Vertical),
        ];
    }

    private async void OnOverlayTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel.PomodoroOverlay?.RefreshCommand.CanExecute(null) == true)
        {
            await _viewModel.PomodoroOverlay.RefreshAsync();
            var snapshot = _viewModel.PomodoroOverlay.Snapshot;
            if (_viewModel.Dashboard?.Settings is { } settings)
                _pomodoroSound.PlayCue(CompletionSound.PomodoroCue(_lastAudioSnapshot, snapshot,
                    settings.BellSoundEnabled, settings.TickSoundEnabled), settings.MasterVolume);
            _lastAudioSnapshot = snapshot;
        }
    }

    private async void OnStopConfirmationRequested(object? sender, EventArgs e)
    {
        if (MessageBox.Show(
                "Dừng phiên Pomodoro hiện tại? Tiến trình chưa hoàn thành sẽ không nhận thưởng.",
                "Xác nhận dừng",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes
            && _viewModel.PomodoroOverlay is not null)
        {
            await _viewModel.PomodoroOverlay.ConfirmStopAsync();
        }
    }

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && ReferenceEquals(e.OriginalSource, sender))
        {
            DragMove();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _windowSource?.RemoveHook(OnWindowMessage);
        _windowHandle = IntPtr.Zero;
        _overlayTimer.Stop();
        _feedbackTimer.Stop();
        _feedbackTimer.Tick -= OnFeedbackTimerTick;
        if (_viewModel.Dashboard is not null) _viewModel.Dashboard.Today.CompletionCommitted -= OnCompletionCommitted;
        _completionSound.Dispose();
        _pomodoroSound.Dispose();
        if (_viewModel.Dashboard is { } dashboard) dashboard.Settings.PropertyChanged -= OnSettingsChanged;
        if (_viewModel.PomodoroOverlay is { } overlay) overlay.PropertyChanged -= OnPomodoroChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ExitRequested -= OnExitRequested;
        _overlayTimer.Tick -= OnOverlayTimerTick;
        LocationChanged -= OnLocationChanged;
        SizeChanged -= OnWindowSizeChanged;
        Closed -= OnClosed;
        if (_viewModel.PomodoroOverlay is not null)
        {
            _viewModel.PomodoroOverlay.StopConfirmationRequested -= OnStopConfirmationRequested;
        }
    }
}
