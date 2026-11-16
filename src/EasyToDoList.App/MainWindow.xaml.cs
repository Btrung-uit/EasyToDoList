using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EasyToDoList.App.Shell;

namespace EasyToDoList.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly WindowModeController _modeController;
    private readonly DispatcherTimer _overlayTimer;
    private bool _exitRequested;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _modeController = new WindowModeController(this);
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _overlayTimer.Tick += OnOverlayTimerTick;
        DataContext = viewModel;
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
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.DisplayMode))
        {
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

    private void OnOverlayTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel.DisplayMode == ShellDisplayMode.Overlay
            && _viewModel.OverlayMode == OverlayMode.Pomodoro
            && _viewModel.PomodoroOverlay?.RefreshCommand.CanExecute(null) == true)
        {
            _viewModel.PomodoroOverlay.RefreshCommand.Execute(null);
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
        _overlayTimer.Tick -= OnOverlayTimerTick;
        LocationChanged -= OnLocationChanged;
        Closed -= OnClosed;
        if (_viewModel.PomodoroOverlay is not null)
        {
            _viewModel.PomodoroOverlay.StopConfirmationRequested -= OnStopConfirmationRequested;
        }
    }
}
