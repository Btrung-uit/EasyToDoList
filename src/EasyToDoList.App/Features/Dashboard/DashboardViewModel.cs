using System.Windows.Input;
using EasyToDoList.App.Features.Calendar;
using EasyToDoList.App.Features.Pomodoro;
using EasyToDoList.App.Features.Profile;
using EasyToDoList.App.Features.Settings;
using EasyToDoList.App.Features.Today;
using EasyToDoList.App.Presentation;
using EasyToDoList.App.Shell;

namespace EasyToDoList.App.Features.Dashboard;

public sealed class DashboardViewModel : ViewModelBase
{
    private object _current;

    public DashboardViewModel(
        TodayViewModel today,
        CalendarViewModel calendar,
        PomodoroViewModel pomodoro,
        ProfileViewModel profile,
        SettingsViewModel settings)
    {
        Today = today; Calendar = calendar; Pomodoro = pomodoro; Profile = profile; Settings = settings;
        _current = today;
        NavigateCommand = new RelayCommand(Navigate);
    }

    public TodayViewModel Today { get; }
    public CalendarViewModel Calendar { get; }
    public PomodoroViewModel Pomodoro { get; }
    public ProfileViewModel Profile { get; }
    public SettingsViewModel Settings { get; }
    public object Current { get => _current; private set => SetField(ref _current, value); }
    public ICommand NavigateCommand { get; }

    public async Task InitializeAsync()
    {
        await Today.LoadAsync();
        await Calendar.LoadAsync();
        await Profile.LoadAsync();
        await Settings.LoadAsync();
    }

    private void Navigate(object? target) => Current = target switch
    {
        "Today" => Today,
        "Calendar" => Calendar,
        "Pomodoro" => Pomodoro,
        "Settings" => Settings,
        _ => Current,
    };
}
