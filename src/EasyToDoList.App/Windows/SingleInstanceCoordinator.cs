using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EasyToDoList.App.Windows;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly Mutex _installationGuard = new(false, @"Local\EasyToDoList-Running");
    private readonly string _pipeName;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listener;
    public bool IsPrimary { get; }

    public SingleInstanceCoordinator(string identity)
    {
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        _pipeName = "EasyToDoList-" + suffix;
        _mutex = new Mutex(false, @"Global\" + _pipeName);
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public async Task ActivateExistingAsync(CancellationToken ct)
    {
        using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(5000, ct);
        if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var processId))
            AllowSetForegroundWindow(processId);
        await pipe.WriteAsync(new byte[] { 1 }, ct);
    }

    public void Listen(Action activate, Action<Exception> reportError)
    {
        if (!IsPrimary || _listener is not null) throw new InvalidOperationException();
        _listener = ListenAsync(activate, reportError);
    }

    private async Task ListenAsync(Action activate, Action<Exception> reportError)
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var signal = new byte[1];
                if (await pipe.ReadAsync(signal, timeout.Token).ConfigureAwait(false) == 1 && signal[0] == 1) activate();
            }
            catch (OperationCanceledException) { }
            catch (IOException error) { reportError(error); }
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _listener?.GetAwaiter().GetResult();
        _shutdown.Dispose();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _installationGuard.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
