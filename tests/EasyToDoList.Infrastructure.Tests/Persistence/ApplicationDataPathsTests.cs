using EasyToDoList.Infrastructure.Persistence;

namespace EasyToDoList.Infrastructure.Tests.Persistence;

public sealed class ApplicationDataPathsTests
{
    [Fact]
    public void DatabasePath_is_under_local_application_data()
    {
        var localApplicationData = Path.GetFullPath(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        var databasePath = Path.GetFullPath(ApplicationDataPaths.DatabasePath);

        Assert.StartsWith(localApplicationData + Path.DirectorySeparatorChar, databasePath);
        Assert.EndsWith(Path.Combine("EasyToDoList", "easytodolist.db"), databasePath);
    }
}
