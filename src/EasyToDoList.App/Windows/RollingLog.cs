using System.IO;

namespace EasyToDoList.App.Windows;

public sealed class RollingLog(string directoryPath)
{
    private readonly object _gate = new();
    private const long MaxBytes = 256 * 1024;
    public string DirectoryPath => directoryPath;

    public void Write(string operation, Exception? error = null)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                var path = Path.Combine(directoryPath, "app.log");
                if (File.Exists(path) && new FileInfo(path).Length >= MaxBytes)
                    File.Move(path, Path.Combine(directoryPath, "app.previous.log"), true);
                // Exception messages/SQL may contain titles: keep only the exception type.
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {operation} {error?.GetType().Name}\n");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
