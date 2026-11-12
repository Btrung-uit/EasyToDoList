namespace EasyToDoList.Core.Todos;

public sealed record CreateTodoCommand(string Title, DateOnly StartDate, DateOnly Deadline);

public sealed record UpdateTodoScheduleCommand(Guid TodoId, DateOnly StartDate, DateOnly Deadline);
