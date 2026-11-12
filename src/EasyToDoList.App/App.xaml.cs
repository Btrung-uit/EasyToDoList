using System.Configuration;
using System.Windows;
using EasyToDoList.Infrastructure.Persistence;

namespace EasyToDoList.App;

public partial class App : Application
{
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

        new MainWindow().Show();
    }
}
