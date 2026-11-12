namespace EasyToDoList.Infrastructure.Persistence;

public static class ApplicationDataPaths
{
    public static string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EasyToDoList",
        "easytodolist.db");
}
