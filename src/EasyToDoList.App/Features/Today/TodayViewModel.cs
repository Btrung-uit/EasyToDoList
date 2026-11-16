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
    private readonly Func<bool> _motionEnabled;
    private readonly HashSet<Guid> _pending = [];
    private readonly SemaphoreSlim _databaseGate = new(1, 1);
    private DateTimeOffset _comboUntil;
    private int _comboGeneration;
    private int _comboCount;
    private double _comboProgress;
    private Guid? _editingId;
    private string _title = string.Empty;
    private DateTime? _startDate;
    private DateTime? _deadline;
    private string? _titleError;
    private string? _deadlineError;
    private string? _errorMessage;
    private bool _isLoading;

    public TodayViewModel(ITodoService service, IClock clock, Func<Task>? afterMutation = null, Func<bool>? motionEnabled = null)
    {
        _service = service;
        _clock = clock;
        _afterMutation = afterMutation;
        _motionEnabled = motionEnabled ?? (() => true);
        StartDate = clock.LocalToday.ToDateTime(TimeOnly.MinValue);
        Deadline = StartDate;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        EditCommand = new RelayCommand(BeginEdit);
        CompleteCommand = new AsyncRelayCommand<TodoRowViewModel>(CompleteAsync);
        CancelCommand = new AsyncRelayCommand<TodoRowViewModel>(CancelAsync);
        UndoCommand = new AsyncRelayCommand<CompletionFeedback>(UndoAsync);
    }

    public ObservableCollection<TodoRowViewModel> Items { get; } = [];
    public ObservableCollection<CompletionFeedback> Feedback { get; } = [];
    public event EventHandler? CompletionCommitted;
    public int ComboCount { get => _comboCount; private set => SetField(ref _comboCount, value); }
    public double ComboProgress { get => _comboProgress; private set => SetField(ref _comboProgress, value); }
    public bool HasCombo => ComboCount > 0;
    public bool IsComboActive => ComboCount >= 2;
    public string ComboLabel => IsComboActive ? $"Combo ×{ComboCount} — Tuyệt lắm!" : "Hoàn thành!";
    public bool HasFeedback => Feedback.Count > 0;
    public bool MotionEnabled => _motionEnabled();
    public string MascotFrame
    {
        get
        {
            var elapsed = 2 - (_comboUntil - _clock.UtcNow).TotalSeconds;
            var (state, frame) = SceneAnimation.Mascot("idle", elapsed, MotionEnabled && HasCombo, HasCombo ? 0 : null);
            if (state == "idle") frame = 0;
            return $"/EasyToDoList.App;component/Assets/Mascots/{state}/{state}-{frame:00}.png";
        }
    }
    public ICommand UndoCommand { get; }
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
            var items = await ReadAsync(() => _service.GetTodayAsync(_clock.LocalToday, CancellationToken.None));
            var ids = items.Select(x => x.Id).ToHashSet();
            foreach (var old in Items.Where(x => !ids.Contains(x.Id) && !x.IsCompleting).ToArray()) Items.Remove(old);
            foreach (var item in items)
            {
                var row = Items.FirstOrDefault(x => x.Id == item.Id);
                if (row is null) Items.Add(new TodoRowViewModel(item));
                else row.Update(item);
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
                await ReadAsync(() => _service.UpdateAsync(new UpdateTodoCommand(id, Title, start, deadline), CancellationToken.None));
            }
            else
            {
                await ReadAsync(() => _service.CreateAsync(new CreateTodoCommand(Title, start, deadline), CancellationToken.None));
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
        if (value is not TodoRowViewModel row || row.IsCompleting || _pending.Contains(row.Id))
        {
            return;
        }
        var item = row.Item;
        _editingId = item.Id;
        Title = item.Title;
        StartDate = item.StartDate.ToDateTime(TimeOnly.MinValue);
        Deadline = item.Deadline.ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(IsEditing));
    }

    public async Task CompleteAsync(TodoRowViewModel row)
    {
        if (row.IsCompleting || !Items.Contains(row) || !_pending.Add(row.Id)) return;
        ErrorMessage = null;
        try
        {
            TodoCompletionResult result;
            try { result = await ReadAsync(() => _service.CompleteAsync(row.Id, CancellationToken.None)); }
            catch (Exception) { ErrorMessage = "Không thể hoàn thành nhiệm vụ. Hãy thử lại."; return; }
            RefreshFeedback();
            if (ComboCount == 0) _comboGeneration++;
            ComboCount++;
            _comboUntil = _clock.UtcNow.AddSeconds(2);
            ComboProgress = 1;
            Feedback.Add(new CompletionFeedback(result, Items.IndexOf(row), _comboGeneration));
            row.AnimationStartedUtc = _clock.UtcNow;
            row.CompletionProgress = 0;
            if (!MotionEnabled) Items.Remove(row);
            NotifyFeedback();
            // Presentation failures cannot undo a committed completion or hide its Undo token.
            try { CompletionCommitted?.Invoke(this, EventArgs.Empty); } catch (Exception) { }
            await RefreshProfileAsync();
        }
        finally { _pending.Remove(row.Id); }
    }

    public async Task UndoAsync(CompletionFeedback feedback)
    {
        var result = feedback.Result;
        if (!Feedback.Contains(feedback) || _clock.UtcNow > result.UndoUntilUtc || !_pending.Add(result.Todo.Id)) return;
        ErrorMessage = null;
        try
        {
            try { await WriteAsync(() => _service.UndoCompletionAsync(result.Todo.Id, result.TransactionId, CancellationToken.None)); }
            catch (Exception) { ErrorMessage = "Không thể hoàn tác. Hãy thử lại trong 5 giây."; return; }
            Feedback.Remove(feedback);
            var row = Items.FirstOrDefault(x => x.Id == result.Todo.Id);
            if (row is null)
            {
                row = new TodoRowViewModel(result.Todo);
                Items.Insert(Math.Min(feedback.OriginalIndex, Items.Count), row);
            }
            row.AnimationStartedUtc = null;
            row.CompletionProgress = 0;
            RefreshFeedback();
            if (feedback.ComboGeneration == _comboGeneration && ComboCount > 0) ComboCount--;
            if (ComboCount == 0) ComboProgress = 0;
            NotifyFeedback();
            await LoadAsync();
            await RefreshProfileAsync();
        }
        finally { _pending.Remove(result.Todo.Id); }
    }

    public void RefreshFeedback()
    {
        if (ComboCount == 0 && Feedback.Count == 0 && !Items.Any(x => x.IsCompleting)) return;
        var now = _clock.UtcNow;
        if (now >= _comboUntil) { ComboCount = 0; ComboProgress = 0; }
        else ComboProgress = Math.Clamp((_comboUntil - now).TotalSeconds / 2, 0, 1);
        foreach (var row in Items.Where(x => x.IsCompleting).ToArray())
        {
            row.CompletionProgress = Math.Clamp((now - row.AnimationStartedUtc!.Value).TotalMilliseconds / 800, 0, 1);
            if (row.CompletionProgress >= 1 || !MotionEnabled) Items.Remove(row);
        }
        foreach (var entry in Feedback.Where(x => now > x.Result.UndoUntilUtc).ToArray()) Feedback.Remove(entry);
        NotifyFeedback();
    }

    private void NotifyFeedback()
    {
        OnPropertyChanged(nameof(HasCombo));
        OnPropertyChanged(nameof(IsComboActive));
        OnPropertyChanged(nameof(ComboLabel));
        OnPropertyChanged(nameof(HasFeedback));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(MascotFrame));
    }

    private async Task RefreshProfileAsync()
    {
        if (_afterMutation is null) return;
        try { await WriteAsync(_afterMutation); }
        catch (Exception) { ErrorMessage = "Đã lưu nhiệm vụ; chưa tải lại được hồ sơ."; }
    }

    private async Task CancelAsync(TodoRowViewModel row)
    {
        if (row.IsCompleting || !_pending.Add(row.Id)) return;
        try { await MutateAsync(() => _service.CancelAsync(row.Id, CancellationToken.None)); }
        finally { _pending.Remove(row.Id); }
    }

    private async Task MutateAsync(Func<Task> mutation)
    {
        ErrorMessage = null;
        try
        {
            await WriteAsync(mutation);
            await LoadAsync();
            if (_afterMutation is not null)
            {
                await WriteAsync(_afterMutation);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể cập nhật nhiệm vụ. Hãy thử lại.";
        }
    }

    // Accept clicks immediately while serializing access to the shared EF context.
    private async Task<T> ReadAsync<T>(Func<Task<T>> operation)
    {
        await _databaseGate.WaitAsync();
        try { return await operation(); }
        finally { _databaseGate.Release(); }
    }

    private async Task WriteAsync(Func<Task> operation)
    {
        await _databaseGate.WaitAsync();
        try { await operation(); }
        finally { _databaseGate.Release(); }
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
