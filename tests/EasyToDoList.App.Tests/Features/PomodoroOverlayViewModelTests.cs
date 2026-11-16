using EasyToDoList.App.Features.Overlay;
using EasyToDoList.Core.Pomodoro;

namespace EasyToDoList.App.Tests.Features;

public sealed class PomodoroOverlayViewModelTests
{
    [Fact]
    public async Task Lifecycle_owned_timer_prevents_overlay_from_awarding_offline_time()
    {
        var service = new PomodoroServiceStub(CreateSnapshot(PomodoroPhase.Focus, PomodoroStatus.Running, 0));
        var viewModel = new PomodoroOverlayViewModel(service, () => PomodoroConfiguration.Standard, lifecycleOwnsTimer: true);
        await viewModel.RefreshAsync();
        Assert.Equal(0, service.TickCount);
        Assert.Equal("00:00", viewModel.TimeText);
    }

    [Theory]
    [InlineData(PomodoroPhase.Focus, "Tập trung")]
    [InlineData(PomodoroPhase.ShortBreak, "Nghỉ ngắn")]
    [InlineData(PomodoroPhase.LongBreak, "Nghỉ dài")]
    public async Task Refresh_projects_service_snapshot_without_owning_time(
        PomodoroPhase phase,
        string expectedPhase)
    {
        var service = new PomodoroServiceStub(CreateSnapshot(phase, PomodoroStatus.Running, 1_200));
        var viewModel = new PomodoroOverlayViewModel(service, () => PomodoroConfiguration.Standard);

        await viewModel.RefreshAsync();

        Assert.Equal(expectedPhase, viewModel.PhaseLabel);
        Assert.Equal("20:00", viewModel.TimeText);
        Assert.Equal("Đang chạy", viewModel.StatusLabel);
        Assert.Equal(0, service.TickCount);
    }

    [Fact]
    public async Task Refresh_settles_elapsed_phase_once_through_core_service()
    {
        var service = new PomodoroServiceStub(
            CreateSnapshot(PomodoroPhase.Focus, PomodoroStatus.Running, 0));
        var viewModel = new PomodoroOverlayViewModel(service, () => PomodoroConfiguration.Standard);

        await viewModel.RefreshAsync();

        Assert.Equal(1, service.TickCount);
        Assert.Equal("Nghỉ ngắn", viewModel.PhaseLabel);
        Assert.Equal("05:00", viewModel.TimeText);
    }

    [Fact]
    public async Task Refresh_failure_keeps_last_snapshot_and_reports_recoverable_error()
    {
        var service = new PomodoroServiceStub(
            CreateSnapshot(PomodoroPhase.Focus, PomodoroStatus.Running, 0))
        {
            TickException = new InvalidOperationException("database unavailable"),
        };
        var viewModel = new PomodoroOverlayViewModel(service, () => PomodoroConfiguration.Standard);

        await viewModel.RefreshAsync();

        Assert.Equal("00:00", viewModel.TimeText);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Concurrent_refresh_does_not_tick_core_service_twice()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new PomodoroServiceStub(
            CreateSnapshot(PomodoroPhase.Focus, PomodoroStatus.Running, 0))
        {
            TickGate = gate.Task,
        };
        var viewModel = new PomodoroOverlayViewModel(service, () => PomodoroConfiguration.Standard);

        var first = viewModel.RefreshAsync();
        var second = viewModel.RefreshAsync();
        try
        {
            Assert.Equal(1, service.TickCount);
        }
        finally
        {
            gate.SetResult();
            await Task.WhenAll(first, second);
        }
    }

    [Fact]
    public async Task Start_uses_dashboard_configuration_and_projects_new_session()
    {
        var configuration = PomodoroConfiguration.DeepFocus;
        var service = new PomodoroServiceStub();
        var viewModel = new PomodoroOverlayViewModel(service, () => configuration);

        await viewModel.StartAsync();

        Assert.Same(configuration, service.StartedWith);
        Assert.Equal("50:00", viewModel.TimeText);
        Assert.Equal("Tập trung", viewModel.PhaseLabel);
    }

    [Fact]
    public async Task Stop_requires_confirmation_before_mutating_session()
    {
        var service = new PomodoroServiceStub(
            CreateSnapshot(PomodoroPhase.Focus, PomodoroStatus.Running, 900));
        var viewModel = new PomodoroOverlayViewModel(service, () => PomodoroConfiguration.Standard);
        var requested = false;
        viewModel.StopConfirmationRequested += (_, _) => requested = true;

        viewModel.RequestStop();

        Assert.True(requested);
        Assert.Equal(0, service.StopCount);

        await viewModel.ConfirmStopAsync();
        Assert.Equal(1, service.StopCount);
        Assert.Equal("Chưa bắt đầu", viewModel.StatusLabel);
    }

    private static PomodoroSnapshot CreateSnapshot(
        PomodoroPhase phase,
        PomodoroStatus status,
        int remainingSeconds) =>
        new(
            Guid.NewGuid(),
            phase,
            status,
            PomodoroConfiguration.Standard,
            phase == PomodoroPhase.Focus ? 1_500 : 300,
            remainingSeconds,
            DateTimeOffset.Parse("2026-11-16T01:00:00Z"),
            status == PomodoroStatus.Running ? DateTimeOffset.Parse("2026-11-16T01:25:00Z") : null,
            status == PomodoroStatus.Paused ? DateTimeOffset.Parse("2026-11-16T01:10:00Z") : null,
            null,
            0,
            null,
            null);

    private sealed class PomodoroServiceStub(PomodoroSnapshot? current = null) : IPomodoroService
    {
        public PomodoroSnapshot? Current { get; private set; } = current;
        public PomodoroConfiguration? StartedWith { get; private set; }
        public int TickCount { get; private set; }
        public int StopCount { get; private set; }
        public Exception? TickException { get; init; }
        public Task? TickGate { get; init; }

        public Task StartAsync(PomodoroConfiguration configuration, CancellationToken cancellationToken)
        {
            StartedWith = configuration;
            Current = CreateSnapshot(
                PomodoroPhase.Focus,
                PomodoroStatus.Running,
                configuration.FocusMinutes * 60) with { Configuration = configuration };
            return Task.CompletedTask;
        }

        public Task PauseAsync(PauseReason reason, CancellationToken cancellationToken)
        {
            Current = Current! with { Status = PomodoroStatus.Paused };
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken)
        {
            Current = Current! with { Status = PomodoroStatus.Running };
            return Task.CompletedTask;
        }

        public Task SkipAsync(CancellationToken cancellationToken)
        {
            Current = Current! with { Status = PomodoroStatus.Skipped };
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            Current = null;
            return Task.CompletedTask;
        }

        public async Task TickAsync(CancellationToken cancellationToken)
        {
            TickCount++;
            if (TickException is not null)
            {
                throw TickException;
            }
            if (TickGate is not null)
            {
                await TickGate;
            }
            Current = CreateSnapshot(PomodoroPhase.ShortBreak, PomodoroStatus.Awaiting, 300);
        }

        public Task RecoverAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
