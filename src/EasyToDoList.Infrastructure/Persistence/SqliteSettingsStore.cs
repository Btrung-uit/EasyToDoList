using System.Text.Json;
using EasyToDoList.Core.Settings;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteSettingsStore(AppDbContext dbContext) : ISettingsStore
{
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var row = await dbContext.AppSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken);
        if (row is null || row.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            return AppSettings.Default;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(row.SettingsJson);
            settings?.Validate();
            return settings ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (ArgumentOutOfRangeException)
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var row = await dbContext.AppSettings.SingleOrDefaultAsync(
            item => item.Id == 1,
            cancellationToken);
        if (row is null)
        {
            row = new AppSettingsRow { Id = 1 };
            dbContext.AppSettings.Add(row);
        }

        row.SchemaVersion = AppSettings.CurrentSchemaVersion;
        row.SettingsJson = JsonSerializer.Serialize(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
