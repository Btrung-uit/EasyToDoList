using System.Windows.Threading;
using Microsoft.Win32;

namespace EasyToDoList.App.Windows;

public sealed class PowerStateService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<Task> _suspend;
    private readonly Func<Task> _resume;
    private readonly Action<Exception> _reportError;
    private bool _disposed;

    public PowerStateService(Dispatcher dispatcher, Func<Task> suspend, Func<Task> resume, Action<Exception> reportError)
    {
        _dispatcher = dispatcher;
        _suspend = suspend;
        _resume = resume;
        _reportError = reportError;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public Task HandleAsync(PowerModes mode)
    {
        if (_disposed || mode == PowerModes.StatusChange) return Task.CompletedTask;
        var operation = mode == PowerModes.Suspend ? _suspend : _resume;
        return _dispatcher.CheckAccess() ? operation() : _dispatcher.InvokeAsync(operation).Task.Unwrap();
    }

    private async void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        try { await HandleAsync(e.Mode); }
        catch (Exception error) { _reportError(error); }
    }

    public void Dispose()
    {
        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
