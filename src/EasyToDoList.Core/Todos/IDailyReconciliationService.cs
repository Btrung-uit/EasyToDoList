namespace EasyToDoList.Core.Todos;

public interface IDailyReconciliationService
{
    Task ReconcileAsync(DateOnly localDate, CancellationToken cancellationToken);
}
