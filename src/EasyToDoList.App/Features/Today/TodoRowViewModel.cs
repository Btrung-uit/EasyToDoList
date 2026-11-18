using EasyToDoList.App.Presentation;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Features.Today;

public sealed class TodoRowViewModel(TodoItem item) : ViewModelBase
{
    private double _completionProgress;
    public TodoItem Item { get; private set; } = item;
    public Guid Id => Item.Id;
    public string Title => Item.Title;
    public DateOnly Deadline => Item.Deadline;
    public string StatusLabel => IsCompleting ? "Đã hoàn thành" : Item.Status == TodoStatus.Overdue ? "Quá hạn" : "Chưa hoàn thành";
    public DateTimeOffset? AnimationStartedUtc { get; internal set; }
    public bool IsCompleting => AnimationStartedUtc is not null;
    public double CompletionProgress
    {
        get => _completionProgress;
        internal set { SetField(ref _completionProgress, value); OnPropertyChanged(nameof(IsCompleting)); OnPropertyChanged(nameof(StatusLabel)); }
    }

    internal void Update(TodoItem value)
    {
        Item = value;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Deadline));
        OnPropertyChanged(nameof(StatusLabel));
    }
}
