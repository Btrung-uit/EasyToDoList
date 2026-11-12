using System.IO;
using EasyToDoList.Infrastructure.Persistence;

namespace EasyToDoList.App;

public static class AppStartup
{
    public static async Task<string?> InitializeDatabaseAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            await new DatabaseInitializer(databasePath).InitializeAsync(cancellationToken);
            return null;
        }
        catch (Exception exception)
        {
            return $"Không thể khởi tạo dữ liệu ứng dụng.\n\nDatabase: {databasePath}\n\n{exception.Message}";
        }
    }
}
