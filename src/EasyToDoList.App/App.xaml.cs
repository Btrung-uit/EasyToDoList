using System.Windows;
using EasyToDoList.App.Shell;
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
        var pomodoroService = new PomodoroService(
            new SqlitePomodoroStore(_dbContext),
            new SqliteRewardStore(_dbContext),
            new SqliteProfileStore(_dbContext),
            new EfAppUnitOfWork(_dbContext),
            new EconomyCalculator(),
            new SystemClock());
        var window = new MainWindow(new ShellViewModel(pomodoroService));
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dbContext?.Dispose();
        base.OnExit(e);
    }
}
