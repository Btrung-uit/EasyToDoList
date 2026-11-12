using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Profile;
using Microsoft.EntityFrameworkCore;

namespace EasyToDoList.Infrastructure.Persistence;

public sealed class SqliteProfileStore(AppDbContext dbContext) : IProfileStore
{
    public async Task<RewardAmount> ApplyAsync(
        RewardAmount requestedChange,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.PlayerProfiles.SingleAsync(
            row => row.Id == 1,
            cancellationToken);
        var previousExperience = profile.TotalExperience;
        var previousCoins = profile.CoinBalance;
        profile.TotalExperience = Math.Max(0, checked(previousExperience + requestedChange.Experience));
        profile.CoinBalance = Math.Max(0, checked(previousCoins + requestedChange.Coins));

        return new RewardAmount(
            checked((int)(profile.TotalExperience - previousExperience)),
            checked((int)(profile.CoinBalance - previousCoins)));
    }
}
