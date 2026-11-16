using System.Collections.ObjectModel;
using System.Windows.Input;
using EasyToDoList.App.Presentation;
using EasyToDoList.App.Shell;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Features.Today;

public sealed class TodayViewModel : ViewModelBase
{
    private readonly ITodoService _service;
    private readonly IClock _clock;
    private readonly Func<Task>? _afterMutation;
    private Guid? _editingId;
    private string _title = string.Empty;
    private DateTime? _startDate;
    private DateTime? _deadline;
    private string? _titleError;
    private string? _deadlineError;
    private string? _errorMessage;
    private bool _isLoading;

    public TodayViewModel(ITodoService service, IClock clock, Func<Task>? afterMutation = null)
    {
        _service = service;
        _clock = clock;
        _afterMutation = afterMutation;
        StartDate = clock.LocalToday.ToDateTime(TimeOnly.MinValue);
        Deadline = StartDate;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        EditCommand = new RelayCommand(BeginEdit);
        CompleteCommand = new AsyncRelayCommand<TodoItem>(CompleteAsync);
        CancelCommand = new AsyncRelayCommand<TodoItem>(CancelAsync);
    }

    public ObservableCollection<TodoItem> Items { get; } = [];
    public string Title { get => _title; set => SetField(ref _title, value); }
    public DateTime? StartDate { get => _startDate; set => SetField(ref _startDate, value); }
    public DateTime? Deadline { get => _deadline; set => SetField(ref _deadline, value); }
    public string? TitleError { get => _titleError; private set => SetField(ref _titleError, value); }
    public string? DeadlineError { get => _deadlineError; private set => SetField(ref _deadlineError, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }
    public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }
    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public bool IsEditing => _editingId is not null;
    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand CancelCommand { get; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var items = await _service.GetTodayAsync(_clock.LocalToday, CancellationToken.None);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể tải nhiệm vụ. Hãy thử lại.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public async Task SaveAsync()
    {
        if (!ValidateForm())
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            var start = DateOnly.FromDateTime(StartDate!.Value);
            var deadline = DateOnly.FromDateTime(Deadline!.Value);
            if (_editingId is Guid id)
            {
                await _service.UpdateAsync(new UpdateTodoCommand(id, Title, start, deadline), CancellationToken.None);
            }
            else
            {
                await _service.CreateAsync(new CreateTodoCommand(Title, start, deadline), CancellationToken.None);
            }
            ResetForm();
            await LoadAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể lưu nhiệm vụ. Hãy thử lại.";
        }
    }

    private void BeginEdit(object? value)
    {
        if (value is not TodoItem item)
        {
            return;
        }
        _editingId = item.Id;
        Title = item.Title;
        StartDate = item.StartDate.ToDateTime(TimeOnly.MinValue);
        Deadline = item.Deadline.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(IsEditing));
    }

    private async Task CompleteAsync(TodoItem item) => await MutateAsync(() => _service.CompleteAsync(item.Id, CancellationToken.None));

    private async Task CancelAsync(TodoItem item) => await MutateAsync(() => _service.CancelAsync(item.Id, CancellationToken.None));

    private async Task MutateAsync(Func<Task> mutation)
    {
        ErrorMessage = null;
        try
        {
            await mutation();
            await LoadAsync();
            if (_afterMutation is not null)
            {
                await _afterMutation();
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể cập nhật nhiệm vụ. Hãy thử lại.";
        }
    }

    private bool ValidateForm()
    {
        TitleError = string.IsNullOrWhiteSpace(Title) ? "Hãy nhập tên nhiệm vụ." : null;
        DeadlineError = StartDate is null || Deadline is null
            ? "Hãy chọn ngày bắt đầu và hạn chót."
            : Deadline < StartDate ? "Hạn chót không được trước ngày bắt đầu." : null;
        return TitleError is null && DeadlineError is null;
    }

    private void ResetForm()
    {
        _editingId = null;
        Title = string.Empty;
        StartDate = _clock.LocalToday.ToDateTime(TimeOnly.MinValue);
        Deadline = StartDate;
        OnPropertyChanged(nameof(IsEditing));
    }
}
