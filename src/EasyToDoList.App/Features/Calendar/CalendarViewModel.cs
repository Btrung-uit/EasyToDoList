using System.Collections.ObjectModel;
using System.Windows.Input;
using EasyToDoList.App.Presentation;
using EasyToDoList.App.Shell;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Features.Calendar;

public sealed class CalendarViewModel : ViewModelBase
{
    private readonly ITodoHistoryQuery _query;
    private DateOnly _weekStart;
    private string? _errorMessage;

    public CalendarViewModel(ITodoHistoryQuery query, IClock clock)
    {
        _query = query;
        _weekStart = StartOfWeek(clock.LocalToday);
        PreviousWeekCommand = new AsyncRelayCommand(() => MoveWeekAsync(-7));
        NextWeekCommand = new AsyncRelayCommand(() => MoveWeekAsync(7));
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ObservableCollection<TodoHistoryEntry> Entries { get; } = [];
    public DateOnly WeekStart { get => _weekStart; private set { if (SetField(ref _weekStart, value)) OnPropertyChanged(nameof(WeekLabel)); } }
    public string WeekLabel => $"{WeekStart:dd/MM} – {WeekStart.AddDays(6):dd/MM/yyyy}";
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }
    public ICommand PreviousWeekCommand { get; }
    public ICommand NextWeekCommand { get; }
    public ICommand LoadCommand { get; }

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            var entries = await _query.GetWeekAsync(WeekStart, CancellationToken.None);
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể tải lịch sử tuần. Hãy thử lại.";
        }
    }

    private async Task MoveWeekAsync(int days)
    {
        WeekStart = WeekStart.AddDays(days);
        await LoadAsync();
    }

    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
