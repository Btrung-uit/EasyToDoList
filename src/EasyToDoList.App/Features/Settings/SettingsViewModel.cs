using System.Windows.Input;
using EasyToDoList.App.Presentation;
using EasyToDoList.App.Shell;
using EasyToDoList.Core.Settings;

namespace EasyToDoList.App.Features.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _store;
    private AppSettings _settings = AppSettings.Default;
    private string? _statusMessage;

    public int MasterVolume { get => _settings.MasterVolume; set { _settings = _settings with { MasterVolume = value }; OnPropertyChanged(); } }
    public bool AnimationsEnabled { get => _settings.AnimationsEnabled; set { _settings = _settings with { AnimationsEnabled = value }; OnPropertyChanged(); } }
    public bool ReducedMotionEnabled { get => _settings.ReducedMotionEnabled; set { _settings = _settings with { ReducedMotionEnabled = value }; OnPropertyChanged(); } }
    public bool PomodoroNotificationsEnabled { get => _settings.PomodoroNotificationsEnabled; set { _settings = _settings with { PomodoroNotificationsEnabled = value }; OnPropertyChanged(); } }
    public bool DueNotificationsEnabled { get => _settings.DueNotificationsEnabled; set { _settings = _settings with { DueNotificationsEnabled = value }; OnPropertyChanged(); } }
    public bool OverdueNotificationsEnabled { get => _settings.OverdueNotificationsEnabled; set { _settings = _settings with { OverdueNotificationsEnabled = value }; OnPropertyChanged(); } }
    public bool AutoStartEnabled { get => _settings.AutoStartEnabled; set { _settings = _settings with { AutoStartEnabled = value }; OnPropertyChanged(); } }
    public bool CompletionSoundEnabled { get => _settings.CompletionSoundEnabled; set { _settings = _settings with { CompletionSoundEnabled = value }; OnPropertyChanged(); } }
    public bool ComboSoundEnabled { get => _settings.ComboSoundEnabled; set { _settings = _settings with { ComboSoundEnabled = value }; OnPropertyChanged(); } }
    public bool TickSoundEnabled { get => _settings.TickSoundEnabled; set { _settings = _settings with { TickSoundEnabled = value }; OnPropertyChanged(); } }
    public bool BellSoundEnabled { get => _settings.BellSoundEnabled; set { _settings = _settings with { BellSoundEnabled = value }; OnPropertyChanged(); } }
    public string? StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public double? OverlayLeft => _settings.OverlayLeft;
    public double? OverlayTop => _settings.OverlayTop;
    public SettingsViewModel(ISettingsStore store)
    {
        _store = store;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public ICommand SaveCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            _settings = await _store.LoadAsync(CancellationToken.None);
            StatusMessage = null;
            NotifyAll();
        }
        catch (Exception)
        {
            StatusMessage = "Không thể tải cài đặt. Đang dùng giá trị mặc định.";
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            await _store.SaveAsync(_settings, CancellationToken.None);
            StatusMessage = "Đã lưu cài đặt.";
        }
        catch (Exception)
        {
            StatusMessage = "Không thể lưu cài đặt. Hãy thử lại.";
        }
    }

    public void UpdateOverlayPosition(double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return;
        }

        _settings = _settings with { OverlayLeft = left, OverlayTop = top };
        OnPropertyChanged(nameof(OverlayLeft));
        OnPropertyChanged(nameof(OverlayTop));
    }

    public Task PersistAsync() => _store.SaveAsync(_settings, CancellationToken.None);

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(MasterVolume)); OnPropertyChanged(nameof(AnimationsEnabled));
        OnPropertyChanged(nameof(ReducedMotionEnabled)); OnPropertyChanged(nameof(PomodoroNotificationsEnabled));
        OnPropertyChanged(nameof(DueNotificationsEnabled)); OnPropertyChanged(nameof(OverdueNotificationsEnabled));
        OnPropertyChanged(nameof(AutoStartEnabled)); OnPropertyChanged(nameof(CompletionSoundEnabled));
        OnPropertyChanged(nameof(ComboSoundEnabled)); OnPropertyChanged(nameof(TickSoundEnabled));
        OnPropertyChanged(nameof(BellSoundEnabled));
    }
}
