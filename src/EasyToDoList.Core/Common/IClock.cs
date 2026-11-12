namespace EasyToDoList.Core.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly LocalToday { get; }
}
