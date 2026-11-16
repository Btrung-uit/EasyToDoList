namespace EasyToDoList.Infrastructure.Persistence;

public sealed class AppSettingsRow
{
    public int Id { get; set; }

    public int SchemaVersion { get; set; }

    public string SettingsJson { get; set; } = string.Empty;
}
