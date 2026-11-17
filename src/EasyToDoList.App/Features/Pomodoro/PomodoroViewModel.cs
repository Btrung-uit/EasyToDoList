using EasyToDoList.App.Presentation;
using EasyToDoList.App.Shell;
using EasyToDoList.Core.Pomodoro;
using System.Windows.Input;

namespace EasyToDoList.App.Features.Pomodoro;

public sealed class PomodoroViewModel : ViewModelBase
{
    private int _focusMinutes = 25;
    private int _shortBreakMinutes = 5;
    private int _longBreakMinutes = 20;
    private int _longBreakInterval = 4;
    private string? _validationMessage;

    public PomodoroViewModel()
    {
        ValidateCommand = new RelayCommand(_ => TryBuildConfiguration());
        SelectPresetCommand = new RelayCommand(SelectPreset);
    }

    public int FocusMinutes { get => _focusMinutes; set { if (SetField(ref _focusMinutes, value)) OnPropertyChanged(nameof(SelectedPreset)); } }
    public int ShortBreakMinutes { get => _shortBreakMinutes; set { if (SetField(ref _shortBreakMinutes, value)) OnPropertyChanged(nameof(SelectedPreset)); } }
    public string SelectedPreset => $"{FocusMinutes}/{ShortBreakMinutes}";
    public int LongBreakMinutes { get => _longBreakMinutes; set => SetField(ref _longBreakMinutes, value); }
    public int LongBreakInterval { get => _longBreakInterval; set => SetField(ref _longBreakInterval, value); }
    public string? ValidationMessage { get => _validationMessage; private set => SetField(ref _validationMessage, value); }
    public ICommand ValidateCommand { get; }
    public ICommand SelectPresetCommand { get; }

    private void SelectPreset(object? preset)
    {
        var configuration = preset switch
        {
            "25/5" => PomodoroConfiguration.Standard,
            "45/10" => new PomodoroConfiguration(45, 10, 20, 4),
            "50/10" => PomodoroConfiguration.DeepFocus,
            _ => null,
        };
        if (configuration is null) return;
        FocusMinutes = configuration.FocusMinutes;
        ShortBreakMinutes = configuration.ShortBreakMinutes;
        LongBreakMinutes = configuration.LongBreakMinutes;
        LongBreakInterval = configuration.LongBreakInterval;
        ValidationMessage = null;
    }

    public PomodoroConfiguration? TryBuildConfiguration()
    {
        try
        {
            var result = new PomodoroConfiguration(FocusMinutes, ShortBreakMinutes, LongBreakMinutes, LongBreakInterval);
            ValidationMessage = null;
            return result;
        }
        catch (ArgumentOutOfRangeException)
        {
            ValidationMessage = "Tập trung 15–60 phút; nghỉ ngắn bằng 20–30%; nghỉ dài 15–30 phút và sau 3–4 phiên.";
            return null;
        }
    }
}
