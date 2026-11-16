using System.Collections.ObjectModel;
using EasyToDoList.App.Presentation;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Profile;

namespace EasyToDoList.App.Features.Profile;

public sealed class ProfileViewModel(IProfileQuery profileQuery, IRewardHistoryQuery rewardQuery) : ViewModelBase
{
    private int _coins;
    private PlayerProgress _progress = PlayerProgress.FromExperience(0);
    private string? _errorMessage;

    public int Coins { get => _coins; private set => SetField(ref _coins, value); }
    public PlayerProgress Progress { get => _progress; private set => SetField(ref _progress, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }
    public ObservableCollection<RewardHistoryEntry> Rewards { get; } = [];

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            var profile = await profileQuery.GetAsync(CancellationToken.None);
            var rewards = await rewardQuery.GetRecentAsync(50, CancellationToken.None);
            Progress = PlayerProgress.FromExperience(profile.TotalExperience);
            Coins = profile.CoinBalance;
            Rewards.Clear();
            foreach (var reward in rewards) Rewards.Add(reward);
        }
        catch (Exception)
        {
            ErrorMessage = "Không thể tải hồ sơ. Hãy thử lại.";
        }
    }
}
