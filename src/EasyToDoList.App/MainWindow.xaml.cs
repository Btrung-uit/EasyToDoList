using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EasyToDoList.App.Shell;
using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeController _modeController;
    private readonly DispatcherTimer _overlayTimer;
    private readonly DispatcherTimer _feedbackTimer;
    private readonly CompletionSound _completionSound = new();
    private readonly CompletionSound _pomodoroSound = new();
    private PomodoroSnapshot? _lastAudioSnapshot;
    private bool _exitRequested;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
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
        Closed += OnClosed;
        _modeController.Apply(
            viewModel.DisplayMode,
            viewModel.Dashboard?.Settings.OverlayLeft,
            viewModel.Dashboard?.Settings.OverlayTop);
        _overlayTimer.Start();
        _feedbackTimer.Start();
    }

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
            UpdateScene();
            _modeController.Apply(
                _viewModel.DisplayMode,
                _viewModel.Dashboard?.Settings.OverlayLeft,
                _viewModel.Dashboard?.Settings.OverlayTop);
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
        if (_viewModel.DisplayMode == ShellDisplayMode.Overlay)
        {
            _viewModel.UpdateOverlayPosition(Left, Top);
        }
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
        Closed -= OnClosed;
        if (_viewModel.PomodoroOverlay is not null)
        {
            _viewModel.PomodoroOverlay.StopConfirmationRequested -= OnStopConfirmationRequested;
        }
    }
}
