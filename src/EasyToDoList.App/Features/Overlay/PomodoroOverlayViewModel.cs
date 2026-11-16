using System.Windows.Input;
using EasyToDoList.App.Presentation;
using EasyToDoList.App.Shell;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Features.Overlay;

public sealed class PomodoroOverlayViewModel : ViewModelBase
{
    private readonly IPomodoroService _service;
    private readonly Func<PomodoroConfiguration?> _configurationProvider;
    private string _phaseLabel = "Sẵn sàng";
    private string _statusLabel = "Chưa bắt đầu";
    private string _timeText = "--:--";
    private string? _errorMessage;
    private PomodoroSnapshot? _snapshot;
    private bool _isBusy;

    public PomodoroOverlayViewModel(
        IPomodoroService service,
        Func<PomodoroConfiguration?> configurationProvider)
    {
        _service = service;
        _configurationProvider = configurationProvider;
        StartCommand = new AsyncRelayCommand(StartAsync);
        PauseCommand = new AsyncRelayCommand(PauseAsync);
        ResumeCommand = new AsyncRelayCommand(ResumeAsync);
        SkipCommand = new AsyncRelayCommand(SkipAsync);
        StopCommand = new RelayCommand(_ => RequestStop());
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApplySnapshot(service.Current);
    }

    public event EventHandler? StopConfirmationRequested;

    public string PhaseLabel { get => _phaseLabel; private set => SetField(ref _phaseLabel, value); }
    public string StatusLabel { get => _statusLabel; private set => SetField(ref _statusLabel, value); }
    public string TimeText { get => _timeText; private set => SetField(ref _timeText, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }
    public bool HasSession => _snapshot is not null;
    public bool IsRunning => _snapshot?.Status == PomodoroStatus.Running;
    public bool IsPaused => _snapshot?.Status == PomodoroStatus.Paused;
    public bool IsAwaiting => _snapshot?.Status == PomodoroStatus.Awaiting;
    public bool CanStart => _snapshot is null || IsAwaiting;
    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RefreshCommand { get; }

    public Task StartAsync()
    {
        var configuration = _configurationProvider();
        if (configuration is null)
        {
            ErrorMessage = "Cấu hình Pomodoro chưa hợp lệ. Hãy chỉnh trong Dashboard.";
            return Task.CompletedTask;
        }

        return ExecuteAsync(ct => _service.StartAsync(configuration, ct));
    }

    public Task PauseAsync() => ExecuteAsync(ct => _service.PauseAsync(PauseReason.User, ct));

    public Task ResumeAsync() => ExecuteAsync(_service.ResumeAsync);

    public Task SkipAsync() => ExecuteAsync(_service.SkipAsync);

    public void RequestStop() => StopConfirmationRequested?.Invoke(this, EventArgs.Empty);

    public Task ConfirmStopAsync() => ExecuteAsync(_service.StopAsync);

    public async Task RefreshAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        var snapshot = _service.Current;
        try
        {
            if (snapshot is { Status: PomodoroStatus.Running, RemainingSeconds: <= 0 })
            {
                await _service.TickAsync(CancellationToken.None);
                snapshot = _service.Current;
            }

            ErrorMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể làm mới Pomodoro. Hãy thử lại.";
        }
        finally
        {
            _isBusy = false;
        }

        ApplySnapshot(snapshot);
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        ErrorMessage = null;
        try
        {
            await operation(CancellationToken.None);
            ApplySnapshot(_service.Current);
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể cập nhật Pomodoro. Hãy thử lại.";
            ApplySnapshot(_service.Current);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ApplySnapshot(PomodoroSnapshot? snapshot)
    {
        if (snapshot?.Status is PomodoroStatus.Stopped or PomodoroStatus.Skipped)
        {
            snapshot = null;
        }

        _snapshot = snapshot;
        PhaseLabel = snapshot?.Phase switch
        {
            PomodoroPhase.Focus => "Tập trung",
            PomodoroPhase.ShortBreak => "Nghỉ ngắn",
            PomodoroPhase.LongBreak => "Nghỉ dài",
            _ => "Sẵn sàng",
        };
        StatusLabel = snapshot?.Status switch
        {
            PomodoroStatus.Running => "Đang chạy",
            PomodoroStatus.Paused => "Đã tạm dừng",
            PomodoroStatus.Awaiting => "Sẵn sàng sang phiên mới",
            _ => "Chưa bắt đầu",
        };
        TimeText = snapshot is null
            ? "--:--"
            : $"{snapshot.RemainingSeconds / 60:00}:{snapshot.RemainingSeconds % 60:00}";
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsAwaiting));
        OnPropertyChanged(nameof(CanStart));
    }
}
