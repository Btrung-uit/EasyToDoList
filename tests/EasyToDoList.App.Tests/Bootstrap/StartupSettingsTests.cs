using EasyToDoList.App.Features.Settings;
using EasyToDoList.Core.Settings;

namespace EasyToDoList.App.Tests.Bootstrap;

public sealed class StartupSettingsTests
{
    [Fact]
    public async Task Exit_persistence_also_synchronizes_auto_start()
    {
        bool? registered = null;
        var settings = new SettingsViewModel(new Store(), value => registered = value);
        settings.AutoStartEnabled = true;
        await settings.PersistAsync();
        Assert.True(registered);
        settings.AutoStartEnabled = false;
        await settings.PersistAsync();
        Assert.False(registered);
    }

    [Fact]
    public async Task Saving_settings_registers_saved_auto_start_and_reports_registration_failure()
    {
        var store = new Store();
        bool? registered = null;
        var settings = new SettingsViewModel(store, enabled => registered = enabled);
        settings.AutoStartEnabled = true;
        await settings.SaveAsync();
        Assert.True(registered);
        Assert.True(store.Value.AutoStartEnabled);

        settings = new SettingsViewModel(store, _ => throw new UnauthorizedAccessException());
        await settings.LoadAsync();
        settings.AutoStartEnabled = false;
        await settings.SaveAsync();
        Assert.False(store.Value.AutoStartEnabled);
        Assert.Contains("không thể cập nhật", settings.StatusMessage);
    }

    [Fact]
    public async Task Failed_settings_save_does_not_change_windows_registration()
    {
        var called = false;
        var settings = new SettingsViewModel(new Store { FailSave = true }, _ => called = true);
        await settings.SaveAsync();
        Assert.False(called);
        Assert.Contains("Không thể lưu", settings.StatusMessage);
    }

    private sealed class Store : ISettingsStore
    {
        public bool FailSave { get; init; }
        public AppSettings Value { get; private set; } = AppSettings.Default;
        public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Value);
        public Task SaveAsync(AppSettings settings, CancellationToken ct)
        {
            if (FailSave) throw new InvalidOperationException();
            Value = settings;
            return Task.CompletedTask;
        }
    }
}
