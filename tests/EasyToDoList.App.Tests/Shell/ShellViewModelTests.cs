using EasyToDoList.App.Shell;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Tests.Shell;

public sealed class ShellViewModelTests
{
    [Fact]
    public void New_shell_starts_in_dashboard_with_todo_overlay_selected()
    {
        var viewModel = new ShellViewModel(new FakePomodoroService());

        Assert.Equal(ShellDisplayMode.Dashboard, viewModel.DisplayMode);
        Assert.Equal(OverlayMode.Todo, viewModel.OverlayMode);
    }

    [Fact]
    public void Switching_modes_keeps_the_existing_pomodoro_session()
    {
        var snapshot = CreateRunningSnapshot();
        var pomodoro = new FakePomodoroService(snapshot);
        var viewModel = new ShellViewModel(pomodoro);

        viewModel.ShowOverlayCommand.Execute(OverlayMode.Pomodoro);
        viewModel.ShowDashboardCommand.Execute(null);

        Assert.Equal(ShellDisplayMode.Dashboard, viewModel.DisplayMode);
        Assert.Equal(OverlayMode.Pomodoro, viewModel.OverlayMode);
        Assert.Same(snapshot, pomodoro.Current);
        Assert.Empty(pomodoro.Operations);
    }

    [Fact]
    public void Closing_the_window_selects_hidden_to_tray()
    {
        var viewModel = new ShellViewModel(new FakePomodoroService());

        viewModel.HideToTray();

        Assert.Equal(ShellDisplayMode.HiddenToTray, viewModel.DisplayMode);
    }

    [Fact]
    public async Task Exit_pauses_a_running_pomodoro_before_requesting_shutdown()
    {
        var order = new List<string>();
        var pomodoro = new FakePomodoroService(CreateRunningSnapshot(), order);
        var viewModel = new ShellViewModel(pomodoro);
        viewModel.ExitRequested += (_, _) => order.Add("exit");

        await viewModel.ExitAsync();

        Assert.Equal(["pause:ApplicationExit", "exit"], order);
    }

    [Fact]
    public async Task Exit_pause_failure_keeps_the_app_open_and_reports_the_problem()
    {
        var pomodoro = new FakePomodoroService(
            CreateRunningSnapshot(),
            pauseException: new InvalidOperationException("database unavailable"));
        var viewModel = new ShellViewModel(pomodoro);
        var exitRequested = false;
        viewModel.ExitRequested += (_, _) => exitRequested = true;

        await viewModel.ExitAsync();

        Assert.False(exitRequested);
        Assert.NotNull(viewModel.ExitErrorMessage);
    }

    private static PomodoroSnapshot CreateRunningSnapshot() =>
        new(
            Guid.NewGuid(),
            PomodoroPhase.Focus,
            PomodoroStatus.Running,
            PomodoroConfiguration.Standard,
            1_500,
            1_200,
            DateTimeOffset.Parse("2026-11-14T01:00:00Z"),
            DateTimeOffset.Parse("2026-11-14T01:25:00Z"),
            null,
            null,
            0,
            null,
            null);

    private sealed class FakePomodoroService(
        PomodoroSnapshot? current = null,
        List<string>? operations = null,
        Exception? pauseException = null) : IPomodoroService
    {
        private readonly List<string> _operations = operations ?? [];

        public PomodoroSnapshot? Current { get; } = current;

        public IReadOnlyList<string> Operations => _operations;

        public Task PauseAsync(PauseReason reason, CancellationToken cancellationToken)
        {
            _operations.Add($"pause:{reason}");
            return pauseException is null
                ? Task.CompletedTask
                : Task.FromException(pauseException);
        }

        public Task StartAsync(PomodoroConfiguration configuration, CancellationToken cancellationToken) =>
            RecordUnexpectedAsync("start");

        public Task ResumeAsync(CancellationToken cancellationToken) => RecordUnexpectedAsync("resume");

        public Task SkipAsync(CancellationToken cancellationToken) => RecordUnexpectedAsync("skip");

        public Task StopAsync(CancellationToken cancellationToken) => RecordUnexpectedAsync("stop");

        public Task TickAsync(CancellationToken cancellationToken) => RecordUnexpectedAsync("tick");

        public Task RecoverAsync(CancellationToken cancellationToken) => RecordUnexpectedAsync("recover");

        private Task RecordUnexpectedAsync(string operation)
        {
            _operations.Add(operation);
            return Task.CompletedTask;
        }
    }
}
