using EasyToDoList.Core.Economy;

namespace EasyToDoList.Core.Profile;

public interface IProfileStore
{
    Task<RewardAmount> ApplyAsync(RewardAmount requestedChange, CancellationToken cancellationToken);
}
