using EasyToDoList.Core.Common;

namespace EasyToDoList.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly LocalToday => DateOnly.FromDateTime(DateTime.Now);
}
