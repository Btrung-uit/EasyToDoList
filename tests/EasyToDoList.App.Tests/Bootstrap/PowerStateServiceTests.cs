using System.Windows.Threading;
using EasyToDoList.App.Windows;
using Microsoft.Win32;

namespace EasyToDoList.App.Tests.Bootstrap;

public sealed class PowerStateServiceTests
{
    [Fact]
    public async Task Power_callback_on_Wpf_context_can_await_without_blocking_dispatcher()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var operations = new List<string>();
                    using var power = new PowerStateService(dispatcher,
                        async () => { await Task.Yield(); operations.Add("suspend"); },
                        async () => { await Task.Yield(); operations.Add("resume"); },
                        error => completed.TrySetException(error));
                    await power.HandleAsync(PowerModes.Suspend);
                    await power.HandleAsync(PowerModes.Resume);
                    Assert.Equal(new[] { "suspend", "resume" }, operations);
                    completed.TrySetResult();
                }
                catch (Exception error) { completed.TrySetException(error); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join();
    }
}
