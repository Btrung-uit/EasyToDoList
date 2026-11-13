using EasyToDoList.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Tests.Persistence;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task Initialize_creates_database_and_single_profile()
    {
        using var directory = new TemporaryDirectory();
        var databasePath = Path.Combine(directory.Path, "test.db");

        await new DatabaseInitializer(databasePath).InitializeAsync(CancellationToken.None);

        var profile = await ReadSingleProfileAsync(databasePath);
        Assert.True(File.Exists(databasePath));
        Assert.Equal(1, profile.Id);
        Assert.Equal(0, profile.TotalExperience);
        Assert.Equal(0, profile.CoinBalance);
    }

    [Fact]
    public async Task Initialize_twice_keeps_single_profile()
    {
        using var directory = new TemporaryDirectory();
        var databasePath = Path.Combine(directory.Path, "test.db");

        var initializer = new DatabaseInitializer(databasePath);
        await initializer.InitializeAsync(CancellationToken.None);
        await initializer.InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PlayerProfiles";
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Initialize_supports_database_path_with_semicolon()
    {
        using var directory = new TemporaryDirectory();
        var databaseDirectory = Path.Combine(directory.Path, "profile;name");
        Directory.CreateDirectory(databaseDirectory);
        var databasePath = Path.Combine(databaseDirectory, "test.db");

        await new DatabaseInitializer(databasePath).InitializeAsync(CancellationToken.None);

        var profile = await ReadSingleProfileAsync(databasePath);
        Assert.Equal(1, profile.Id);
    }

    [Fact]
    public async Task Initialize_creates_domain_tables_and_indexes()
    {
        using var directory = new TemporaryDirectory();
        var databasePath = Path.Combine(directory.Path, "test.db");

        await new DatabaseInitializer(databasePath).InitializeAsync(CancellationToken.None);

        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        Assert.True(await SchemaObjectExistsAsync(connection, "table", "TodoItems"));
        Assert.True(await SchemaObjectExistsAsync(connection, "table", "RewardTransactions"));
        Assert.True(await SchemaObjectExistsAsync(connection, "table", "PomodoroSessions"));
        Assert.True(await SchemaObjectExistsAsync(
            connection,
            "index",
            "IX_TodoItems_Status_StartDate_Deadline"));
        Assert.True(await SchemaObjectExistsAsync(
            connection,
            "index",
            "IX_TodoItems_CompletedAtUtc"));
        Assert.True(await SchemaObjectExistsAsync(
            connection,
            "index",
            "IX_RewardTransactions_CreatedAtUtc"));
        Assert.True(await SchemaObjectExistsAsync(
            connection,
            "index",
            "IX_PomodoroSessions_Status"));

        await using var uniqueCommand = connection.CreateCommand();
        uniqueCommand.CommandText = """
            SELECT [unique]
            FROM pragma_index_list('RewardTransactions')
            WHERE name = 'IX_RewardTransactions_IdempotencyKey'
            """;
        Assert.Equal(1L, (long)(await uniqueCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public void Model_has_no_pending_migration_changes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new AppDbContext(options);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<(long Id, long TotalExperience, long CoinBalance)> ReadSingleProfileAsync(
        string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, TotalExperience, CoinBalance FROM PlayerProfiles";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var profile = (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
        Assert.False(await reader.ReadAsync());
        return profile;
    }

    private static async Task<bool> SchemaObjectExistsAsync(
        SqliteConnection connection,
        string type,
        string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = $type AND name = $name";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name);
        return (long)(await command.ExecuteScalarAsync())! == 1;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"EasyToDoList-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(Path, recursive: true);
        }
    }
}
