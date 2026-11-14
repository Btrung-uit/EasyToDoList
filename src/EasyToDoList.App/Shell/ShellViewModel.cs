using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Shell;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly IPomodoroService _pomodoroService;
    private ShellDisplayMode _displayMode = ShellDisplayMode.Dashboard;
    private OverlayMode _overlayMode = OverlayMode.Todo;
    private string? _exitErrorMessage;

    public ShellViewModel(IPomodoroService pomodoroService)
    {
        _pomodoroService = pomodoroService;
        ShowDashboardCommand = new RelayCommand(_ => DisplayMode = ShellDisplayMode.Dashboard);
        ShowOverlayCommand = new RelayCommand(ShowOverlay);
        HideToTrayCommand = new RelayCommand(_ => HideToTray());
        ExitCommand = new AsyncRelayCommand(ExitAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ExitRequested;

    public ShellDisplayMode DisplayMode
    {
        get => _displayMode;
        private set => SetField(ref _displayMode, value);
    }

    public OverlayMode OverlayMode
    {
        get => _overlayMode;
        private set => SetField(ref _overlayMode, value);
    }

    public string? ExitErrorMessage
    {
        get => _exitErrorMessage;
        private set => SetField(ref _exitErrorMessage, value);
    }

    public ICommand ShowDashboardCommand { get; }

    public ICommand ShowOverlayCommand { get; }

    public ICommand HideToTrayCommand { get; }

    public ICommand ExitCommand { get; }

    public void HideToTray() => DisplayMode = ShellDisplayMode.HiddenToTray;

    public async Task ExitAsync()
    {
        ExitErrorMessage = null;
        if (_pomodoroService.Current?.Status == PomodoroStatus.Running)
        {
            try
            {
                await _pomodoroService.PauseAsync(PauseReason.ApplicationExit, CancellationToken.None);
            }
            catch (Exception)
            {
                ExitErrorMessage = "Không thể lưu trạng thái Pomodoro. Hãy thử thoát lại.";
                return;
            }
        }

        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowOverlay(object? parameter)
    {
        if (parameter is OverlayMode overlayMode)
        {
            OverlayMode = overlayMode;
        }

        DisplayMode = ShellDisplayMode.Overlay;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
