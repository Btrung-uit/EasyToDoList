using System.Windows;
using EasyToDoList.App.Shell;
using EasyToDoList.App.Features.Calendar;
using EasyToDoList.App.Features.Dashboard;
using EasyToDoList.App.Features.Profile;
using EasyToDoList.App.Features.Settings;
using EasyToDoList.App.Features.Today;
using DashboardPomodoroViewModel = EasyToDoList.App.Features.Pomodoro.PomodoroViewModel;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Pomodoro;
using EasyToDoList.Infrastructure.Persistence;
using EasyToDoList.Infrastructure.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.App;

public partial class App : Application
{
    private AppDbContext? _dbContext;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databasePath = ApplicationDataPaths.DatabasePath;
        var startupError = await AppStartup.InitializeDatabaseAsync(
            databasePath,
            CancellationToken.None);
        if (startupError is not null)
        {
            MessageBox.Show(
                startupError,
                "EasyToDoList - Lỗi khởi động",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        var clock = new SystemClock();
        var rewardStore = new SqliteRewardStore(_dbContext);
        var profileStore = new SqliteProfileStore(_dbContext);
        var unitOfWork = new EfAppUnitOfWork(_dbContext);
        var economy = new EconomyCalculator();
        var todoStore = new SqliteTodoStore(_dbContext);
        try
        {
            await new EasyToDoList.Core.Todos.DailyReconciliationService(
                todoStore, rewardStore, profileStore, unitOfWork, economy, clock)
                .ReconcileAsync(clock.LocalToday, CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Không thể cập nhật nhiệm vụ sang ngày mới.\n\n{exception.Message}",
                "EasyToDoList - Lỗi dữ liệu",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        var pomodoroService = new PomodoroService(
            new SqlitePomodoroStore(_dbContext),
            rewardStore, profileStore, unitOfWork, economy, clock);
        var profile = new ProfileViewModel(
            new SqliteProfileQuery(_dbContext),
            new SqliteRewardHistoryQuery(_dbContext));
        var today = new TodayViewModel(
            new EasyToDoList.Core.Todos.TodoService(
                todoStore, rewardStore, profileStore, unitOfWork, economy, clock),
            clock,
            profile.LoadAsync);
        var dashboard = new DashboardViewModel(
            today,
            new CalendarViewModel(new SqliteTodoHistoryQuery(_dbContext), clock),
            new DashboardPomodoroViewModel(),
            profile,
            new SettingsViewModel(new SqliteSettingsStore(_dbContext)));
        await dashboard.InitializeAsync();
        var window = new MainWindow(new ShellViewModel(pomodoroService, dashboard));
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dbContext?.Dispose();
        base.OnExit(e);
    }
}
