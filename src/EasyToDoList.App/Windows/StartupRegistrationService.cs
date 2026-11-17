using Microsoft.Win32;

namespace EasyToDoList.App.Windows;

public sealed class StartupRegistrationService
{
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
            key.SetValue("EasyToDoList", $"\"{executable}\" --tray");
        }
        else key.DeleteValue("EasyToDoList", false);
    }
}
