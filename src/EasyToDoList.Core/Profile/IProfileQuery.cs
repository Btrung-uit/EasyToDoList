namespace EasyToDoList.Core.Profile;

public sealed record ProfileSnapshot(int TotalExperience, int CoinBalance);

public interface IProfileQuery
{
    Task<ProfileSnapshot> GetAsync(CancellationToken cancellationToken);
}
