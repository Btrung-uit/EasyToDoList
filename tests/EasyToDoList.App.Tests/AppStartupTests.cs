using EasyToDoList.App;

namespace EasyToDoList.App.Tests;

public sealed class AppStartupTests
{
    [Fact]
    public async Task InitializeDatabaseAsync_returns_error_with_database_path()
    {
        var blockingFile = Path.GetTempFileName();
        var databasePath = Path.Combine(blockingFile, "easytodolist.db");

        try
        {
            var error = await AppStartup.InitializeDatabaseAsync(
                databasePath,
                CancellationToken.None);

            Assert.NotNull(error);
            Assert.Contains(databasePath, error);
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }
}
