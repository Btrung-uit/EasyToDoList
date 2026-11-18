using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using EasyToDoList.App.Bootstrap;
using EasyToDoList.App.Windows;
using EasyToDoList.App.Shell;
using EasyToDoList.App.Features.Calendar;
using EasyToDoList.App.Features.Dashboard;
using EasyToDoList.App.Features.Profile;
using EasyToDoList.App.Features.Settings;
using EasyToDoList.App.Features.Today;
using EasyToDoList.App.Features.Overlay;
using DashboardPomodoroViewModel = EasyToDoList.App.Features.Pomodoro.PomodoroViewModel;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Core.Todos;
using EasyToDoList.Infrastructure.Persistence;
using EasyToDoList.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.App;

public partial class App : Application
{
    private AppDbContext? _dbContext;
    private AppDbContext? _pomodoroContext;
    private AppDbContext? _lifecycleContext;
    private SingleInstanceCoordinator? _instance;
    private SystemTrayService? _tray;
    private AppLifecycleCoordinator? _lifecycle;
    private DispatcherTimer? _timer;
    private ShellViewModel? _shell;
    private RollingLog? _log;
    private bool _ticking;
    private bool _activationRequested;
    private DateOnly _displayedDate;
    private DateTimeOffset? _displayedCompletion;
    private IPomodoroService? _pomodoro;
    private PowerStateService? _power;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var databasePath = ApplicationDataPaths.DatabasePath;
        _log = new RollingLog(Path.Combine(Path.GetDirectoryName(databasePath)!, "Logs"));
        try
        {
            _instance = new SingleInstanceCoordinator(WindowsIdentity.GetCurrent().User!.Value);
            if (!_instance.IsPrimary)
            {
                await _instance.ActivateExistingAsync(CancellationToken.None);
                Shutdown();
                return;
            }
            _instance.Listen(() => Dispatcher.BeginInvoke(ActivateDashboard), error => _log.Write("activation", error));
            var startupError = await AppStartup.InitializeDatabaseAsync(databasePath, CancellationToken.None);
            if (startupError is not null) throw new InvalidOperationException(startupError);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()).Options;
            _dbContext = new AppDbContext(options);
            _pomodoroContext = new AppDbContext(options);
            _lifecycleContext = new AppDbContext(options);
            var clock = new SystemClock();
            var economy = new EconomyCalculator();
            var rewardStore = new SqliteRewardStore(_dbContext);
            var profileStore = new SqliteProfileStore(_dbContext);
            var unitOfWork = new EfAppUnitOfWork(_dbContext);
            var todoStore = new SqliteTodoStore(_dbContext);
            var pomodoroService = new SerializedPomodoroService(new PomodoroService(
                new SqlitePomodoroStore(_pomodoroContext), new SqliteRewardStore(_pomodoroContext),
                new SqliteProfileStore(_pomodoroContext), new EfAppUnitOfWork(_pomodoroContext), economy, clock));
            _pomodoro = pomodoroService;
            var lifecycleTodos = new SqliteTodoStore(_lifecycleContext);
            var registration = new StartupRegistrationService();
            var settings = new SettingsViewModel(new SqliteSettingsStore(_dbContext), registration.SetEnabled);
            _lifecycle = new AppLifecycleCoordinator(pomodoroService, new DailyReconciliationService(
                lifecycleTodos, new SqliteRewardStore(_lifecycleContext), new SqliteProfileStore(_lifecycleContext),
                new EfAppUnitOfWork(_lifecycleContext), economy, clock), clock,
                () => lifecycleTodos.GetTodayAsync(clock.LocalToday, CancellationToken.None),
                () => settings.Snapshot, (title, body) => _tray?.Notify(title, body));
            await _lifecycle.InitializeAsync();
            var profile = new ProfileViewModel(new SqliteProfileQuery(_dbContext));
            var today = new TodayViewModel(new TodoService(todoStore, rewardStore, profileStore, unitOfWork, economy, clock),
                clock, profile.LoadAsync, () => settings.AnimationsEnabled && !settings.ReducedMotionEnabled,
                new SqliteTodoHistoryQuery(_dbContext));
            var dashboardPomodoro = new DashboardPomodoroViewModel();
            var dashboard = new DashboardViewModel(today, new CalendarViewModel(new SqliteTodoHistoryQuery(_dbContext), clock),
                dashboardPomodoro, profile, settings);
            await dashboard.InitializeAsync();
            _displayedDate = clock.LocalToday;
            _displayedCompletion = pomodoroService.Current?.CompletedAtUtc;
            try { registration.SetEnabled(settings.AutoStartEnabled); }
            catch (Exception error) { _log.Write("startup-registration", error); }
            var overlay = new PomodoroOverlayViewModel(pomodoroService, dashboardPomodoro.TryBuildConfiguration, lifecycleOwnsTimer: true);
            _shell = new ShellViewModel(pomodoroService, dashboard, overlay, _lifecycle.StopAsync);
            var window = new MainWindow(_shell);
            MainWindow = window;
            _shell.ExitRequested += (_, _) => Shutdown();
            _tray = new SystemTrayService(
                () => Dispatcher.Invoke(() => _shell.ShowOverlayCommand.Execute(null)),
                () => Dispatcher.Invoke(ActivateDashboard),
                () => Dispatcher.Invoke(() => _shell.ExitCommand.Execute(null)));
            _power = new PowerStateService(Dispatcher, _lifecycle.SuspendAsync, async () =>
            {
                await _lifecycle.ResumeAsync();
                await dashboard.Today.LoadAsync();
                await dashboard.Calendar.LoadAsync();
                await dashboard.Profile.LoadAsync();
            }, error => _log.Write("power-state", error));
            SessionEnding += OnSessionEnding;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnLifecycleTick;
            _timer.Start();
            window.Show();
            if (e.Args.Contains("--tray") && !_activationRequested) _shell.HideToTray();
            _log.Write("startup");
        }
        catch (Exception error)
        {
            _log.Write("startup-failed", error);
            MessageBox.Show($"Không thể khởi động EasyToDoList.\n\nDatabase: {databasePath}\nLog: {_log.DirectoryPath}\n\n{error.Message}",
                "EasyToDoList - Lỗi khởi động", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ActivateDashboard()
    {
        _activationRequested = true;
        if (_shell is null || MainWindow is null) return;
        _shell.ShowDashboardCommand.Execute(null);
        MainWindow.Show();
        if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    private async void OnLifecycleTick(object? sender, EventArgs e)
    {
        if (_ticking || _lifecycle is null) return;
        _ticking = true;
        try
        {
            await _lifecycle.TickAsync();
            if (_shell?.Dashboard is { } dashboard)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var dateChanged = _displayedDate != today;
                if (dateChanged)
                {
                    await dashboard.Today.LoadAsync();
                    await dashboard.Calendar.LoadAsync();
                    _displayedDate = today;
                }
                if (_displayedCompletion != _pomodoro?.Current?.CompletedAtUtc || dateChanged)
                {
                    await dashboard.Profile.LoadAsync();
                    _displayedCompletion = _pomodoro?.Current?.CompletedAtUtc;
                }
            }
        }
        catch (Exception error) { _log?.Write("lifecycle-tick", error); }
        finally { _ticking = false; }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        // shortcut: forced Windows shutdown may recover the last 30-second checkpoint; explicit Exit persists exactly.
        _lifecycle?.Stop();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _lifecycle?.Stop();
        _power?.Dispose();
        SessionEnding -= OnSessionEnding;
        _tray?.Dispose();
        _instance?.Dispose();
        _lifecycleContext?.Dispose();
        _pomodoroContext?.Dispose();
        _dbContext?.Dispose();
        _log?.Write("exit");
        base.OnExit(e);
    }
}
