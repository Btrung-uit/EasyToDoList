using EasyToDoList.App.Features.Calendar;
using EasyToDoList.App.Features.Pomodoro;
using EasyToDoList.App.Features.Settings;
using EasyToDoList.App.Features.Today;
using EasyToDoList.Core.Common;
using EasyToDoList.Core.Settings;
using EasyToDoList.Core.Todos;

namespace EasyToDoList.App.Tests.Features;

public sealed class DashboardViewModelTests
{
    [Theory]
    [InlineData("25/5", 25, 5, 20)]
    [InlineData("45/10", 45, 10, 20)]
    [InlineData("50/10", 50, 10, 25)]
    public void Presets_replace_custom_values_with_valid_configuration(string preset, int focus, int rest, int longRest)
    {
        var vm = new PomodoroViewModel { FocusMinutes = 40, ShortBreakMinutes = 1, LongBreakInterval = 3 };
        Assert.Null(vm.TryBuildConfiguration());
        vm.SelectPresetCommand.Execute(preset);
        var configuration = Assert.IsType<EasyToDoList.Core.Pomodoro.PomodoroConfiguration>(vm.TryBuildConfiguration());
        Assert.Equal(focus, configuration.FocusMinutes);
        Assert.Equal(rest, configuration.ShortBreakMinutes);
        Assert.Equal(longRest, configuration.LongBreakMinutes);
        Assert.Equal(4, configuration.LongBreakInterval);
        Assert.Equal(preset, vm.SelectedPreset);
        Assert.Null(vm.ValidationMessage);
    }

    [Fact]
    public void Custom_focus_or_break_clears_preset_match_and_restoring_values_reselects_it()
    {
        var vm = new PomodoroViewModel();
        Assert.Equal("25/5", vm.SelectedPreset);
        vm.FocusMinutes = 40;
        Assert.Equal("40/5", vm.SelectedPreset);
        vm.FocusMinutes = 25;
        Assert.Equal("25/5", vm.SelectedPreset);
        vm.ShortBreakMinutes = 6;
        Assert.Equal("25/6", vm.SelectedPreset);
    }

    [Fact]
    public async Task Today_form_reports_field_specific_errors_before_calling_service()
    {
        var service = new TodoServiceStub();
        var viewModel = new TodayViewModel(service, new ClockStub());
        viewModel.Title = " ";
        viewModel.StartDate = new DateTime(2026, 11, 16);
        viewModel.Deadline = new DateTime(2026, 11, 15);

        await viewModel.SaveAsync();

        Assert.Equal("Hãy nhập tên nhiệm vụ.", viewModel.TitleError);
        Assert.Equal("Hạn chót không được trước ngày bắt đầu.", viewModel.DeadlineError);
        Assert.Equal(0, service.WriteCount);
    }

    [Fact]
    public void Pomodoro_rejects_unhealthy_ratio_and_accepts_standard_configuration()
    {
        var viewModel = new PomodoroViewModel { FocusMinutes = 40, ShortBreakMinutes = 1 };

        Assert.Null(viewModel.TryBuildConfiguration());
        Assert.NotNull(viewModel.ValidationMessage);

        viewModel.FocusMinutes = 25;
        viewModel.ShortBreakMinutes = 5;
        Assert.NotNull(viewModel.TryBuildConfiguration());
        Assert.Null(viewModel.ValidationMessage);
    }

    [Fact]
    public async Task Settings_load_and_save_round_trip_view_model_values()
    {
        var store = new SettingsStoreStub(AppSettings.Default with { MasterVolume = 30 });
        var viewModel = new SettingsViewModel(store);

        await viewModel.LoadAsync();
        viewModel.MasterVolume = 45;
        viewModel.ReducedMotionEnabled = true;
        await viewModel.SaveAsync();

        Assert.Equal(45, store.Value.MasterVolume);
        Assert.True(store.Value.ReducedMotionEnabled);
        Assert.Equal("Đã lưu cài đặt.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Overlay_position_is_preserved_when_settings_are_persisted()
    {
        var store = new SettingsStoreStub(AppSettings.Default);
        var viewModel = new SettingsViewModel(store);
        await viewModel.LoadAsync();

        viewModel.UpdateOverlayPosition(720, 180);
        await viewModel.PersistAsync();

        Assert.Equal(720, store.Value.OverlayLeft);
        Assert.Equal(180, store.Value.OverlayTop);
    }

    [Fact]
    public void Calendar_starts_on_monday_of_current_week()
    {
        var viewModel = new CalendarViewModel(new HistoryQueryStub(), new ClockStub());

        Assert.Equal(new DateOnly(2026, 11, 9), viewModel.WeekStart);
    }

    private sealed class ClockStub : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
        public DateOnly LocalToday => new(2026, 11, 15);
    }

    private sealed class TodoServiceStub : ITodoService
    {
        public int WriteCount { get; private set; }
        public Task<TodoItem> CreateAsync(CreateTodoCommand command, CancellationToken cancellationToken) { WriteCount++; throw new NotSupportedException(); }
        public Task<TodoItem> UpdateAsync(UpdateTodoCommand command, CancellationToken cancellationToken) { WriteCount++; throw new NotSupportedException(); }
        public Task<TodoItem> UpdateScheduleAsync(UpdateTodoScheduleCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TodoCompletionResult> CompleteAsync(Guid todoId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UndoCompletionAsync(Guid todoId, Guid completionTransactionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelAsync(Guid todoId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TodoItem>> GetTodayAsync(DateOnly localDate, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TodoItem>>([]);
    }

    private sealed class SettingsStoreStub(AppSettings value) : ISettingsStore
    {
        public AppSettings Value { get; private set; } = value;
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Value);
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) { Value = settings; return Task.CompletedTask; }
    }

    private sealed class HistoryQueryStub : ITodoHistoryQuery
    {
        public Task<IReadOnlyList<TodoHistoryEntry>> GetWeekAsync(DateOnly weekStart, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TodoHistoryEntry>>([]);
    }
}
