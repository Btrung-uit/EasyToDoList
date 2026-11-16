using System.IO;
using EasyToDoList.App.Windows;

namespace EasyToDoList.App.Tests.Bootstrap;

public sealed class WindowsAdapterTests
{
    [Fact]
    public async Task Second_instance_signals_primary_and_does_not_take_ownership()
    {
        var identity = Guid.NewGuid().ToString();
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                using var primary = new SingleInstanceCoordinator(identity);
                Assert.True(primary.IsPrimary);
                primary.Listen(() => activated.TrySetResult(), error => activated.TrySetException(error));
                ready.SetResult();
                Assert.True(stop.Wait(TimeSpan.FromSeconds(15)));
                finished.SetResult();
            }
            catch (Exception error) { ready.TrySetException(error); finished.TrySetException(error); }
        });
        thread.IsBackground = true;
        thread.Start();
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // Mutex ownership is thread-affine; dispose the secondary synchronously on its acquiring thread.
            using var secondary = new SingleInstanceCoordinator(identity);
            Assert.False(secondary.IsPrimary);
            await secondary.ActivateExistingAsync(CancellationToken.None);
            await activated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { stop.Set(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)); thread.Join(); }
        using var next = new SingleInstanceCoordinator(identity);
        Assert.True(next.IsPrimary);
    }

    [Fact]
    public void Rolling_log_rotates_and_never_writes_exception_message()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EasyToDoList-log-" + Guid.NewGuid());
        try
        {
            var log = new RollingLog(directory);
            log.Write("startup", new IOException("private title"));
            var path = Path.Combine(directory, "app.log");
            Assert.DoesNotContain("private title", File.ReadAllText(path));
            File.WriteAllText(path, new string('x', 256 * 1024));
            log.Write("exit");
            Assert.True(File.Exists(Path.Combine(directory, "app.previous.log")));
            Assert.True(new FileInfo(path).Length < 1024);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
