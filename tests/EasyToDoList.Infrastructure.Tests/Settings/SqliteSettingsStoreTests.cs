using EasyToDoList.Core.Settings;
using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Tests.Settings;

public sealed class SqliteSettingsStoreTests
{
    [Fact]
    public async Task Save_then_load_round_trips_current_settings_in_one_row()
    {
        await using var fixture = await SettingsFixture.CreateAsync();
        var store = new SqliteSettingsStore(fixture.Db);
        var settings = AppSettings.Default with
        {
            MasterVolume = 35,
            ReducedMotionEnabled = true,
            AutoStartEnabled = true,
            OverlayLeft = 720,
            OverlayTop = 180,
        };

        await store.SaveAsync(settings, CancellationToken.None);
        await store.SaveAsync(settings with { MasterVolume = 40 }, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(40, loaded.MasterVolume);
        Assert.True(loaded.ReducedMotionEnabled);
        Assert.True(loaded.AutoStartEnabled);
        Assert.Equal(720, loaded.OverlayLeft);
        Assert.Equal(180, loaded.OverlayTop);
        Assert.Equal(1, await fixture.Db.AppSettings.CountAsync());
    }

    [Theory]
    [InlineData("{broken-json", AppSettings.CurrentSchemaVersion)]
    [InlineData("{}", 999)]
    public async Task Invalid_payload_returns_defaults_without_overwriting_stored_data(
        string json,
        int schemaVersion)
    {
        await using var fixture = await SettingsFixture.CreateAsync();
        fixture.Db.AppSettings.Add(new AppSettingsRow
        {
            Id = 1,
            SchemaVersion = schemaVersion,
            SettingsJson = json,
        });
        await fixture.Db.SaveChangesAsync();

        var loaded = await new SqliteSettingsStore(fixture.Db).LoadAsync(CancellationToken.None);

        Assert.Equal(AppSettings.Default, loaded);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.AppSettings.SingleAsync();
        Assert.Equal(json, stored.SettingsJson);
        Assert.Equal(schemaVersion, stored.SchemaVersion);
    }

    private sealed class SettingsFixture(SqliteConnection connection, AppDbContext db) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;

        public static async Task<SettingsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();
            return new SettingsFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
