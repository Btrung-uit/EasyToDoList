using System.Configuration;
using System.IO;
using System.Windows;
using EasyToDoList.Infrastructure.Persistence;

namespace EasyToDoList.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databasePath = ApplicationDataPaths.DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await new DatabaseInitializer(databasePath).InitializeAsync(CancellationToken.None);

        new MainWindow().Show();
    }
}
