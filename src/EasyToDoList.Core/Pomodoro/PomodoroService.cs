using EasyToDoList.Core.Common;
using EasyToDoList.Core.Economy;
using EasyToDoList.Core.Profile;

namespace EasyToDoList.Core.Pomodoro;

public sealed class PomodoroService(
    IPomodoroStore pomodoroStore,
    IRewardStore rewardStore,
    IProfileStore profileStore,
    IAppUnitOfWork unitOfWork,
    EconomyCalculator economyCalculator,
    IClock clock) : IPomodoroService
{
    private PomodoroSession? _current;

    public PomodoroSnapshot? Current => _current?.GetSnapshot(clock.UtcNow);

    public Task StartAsync(
        PomodoroConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ExecuteAndRefreshAsync(async ct =>
        {
            var session = await pomodoroStore.GetActiveAsync(ct);
            if (session is null)
            {
                session = PomodoroSession.Start(Guid.NewGuid(), configuration, clock.UtcNow);
                await pomodoroStore.AddAsync(session, ct);
            }
            else if (session.Status == PomodoroStatus.Awaiting)
            {
                session.StartAwaiting(clock.UtcNow);
            }
            else
            {
                throw new InvalidOperationException("A Pomodoro phase is already active.");
            }

            return session;
        }, cancellationToken);
    }

    public Task PauseAsync(PauseReason reason, CancellationToken cancellationToken) =>
        MutateAsync(session => session.Pause(clock.UtcNow, reason), cancellationToken);

    public Task ResumeAsync(CancellationToken cancellationToken) =>
        MutateAsync(session => session.Resume(clock.UtcNow), cancellationToken);

    public Task SkipAsync(CancellationToken cancellationToken) =>
        MutateAsync(session => session.Skip(clock.UtcNow), cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        MutateAsync(session => session.Stop(clock.UtcNow), cancellationToken);

    public Task TickAsync(CancellationToken cancellationToken) =>
        ExecuteAndRefreshAsync(async ct =>
        {
            var session = await GetRequiredActiveAsync(ct);
            if (session.Status != PomodoroStatus.Running)
            {
                return session;
            }

            var completedPhase = session.CompleteIfElapsed(clock.UtcNow);
            if (completedPhase is null)
            {
                session.Checkpoint(clock.UtcNow);
                return session;
            }

            if (completedPhase == PomodoroPhase.Focus)
            {
                await ApplyFocusRewardAsync(session, ct);
            }

            return session;
        }, cancellationToken);

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            _current = await unitOfWork.ExecuteAsync(async ct =>
            {
                var session = await pomodoroStore.GetActiveAsync(ct);
                if (session?.Status == PomodoroStatus.Running)
                {
                    session.RecoverAsPaused(clock.UtcNow);
                }

                return session;
            }, cancellationToken);
        }
        catch
        {
            _current = await pomodoroStore.GetActiveAsync(CancellationToken.None);
            throw;
        }
    }

    private Task MutateAsync(
        Action<PomodoroSession> mutation,
        CancellationToken cancellationToken) =>
        ExecuteAndRefreshAsync(async ct =>
        {
            var session = await GetRequiredActiveAsync(ct);
            mutation(session);
            return session;
        }, cancellationToken);

    private async Task ApplyFocusRewardAsync(
        PomodoroSession session,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"pomodoro:{session.Id:N}:focus:{session.CompletedFocusCount}";
        var existing = await rewardStore.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            session.MarkRewardApplied(existing.CreatedAtUtc);
            return;
        }

        var reward = economyCalculator.CalculatePomodoroReward(session.FocusMinutes);
        var appliedReward = await profileStore.ApplyAsync(reward, cancellationToken);
        await rewardStore.AddAsync(
            RewardTransaction.Create(
                Guid.NewGuid(),
                RewardSourceType.Pomodoro,
                session.Id,
                RewardReason.Completion,
                appliedReward,
                idempotencyKey,
                clock.UtcNow),
            cancellationToken);
        session.MarkRewardApplied(clock.UtcNow);
    }

    private async Task<PomodoroSession> GetRequiredActiveAsync(CancellationToken cancellationToken) =>
        await pomodoroStore.GetActiveAsync(cancellationToken)
        ?? throw new InvalidOperationException("There is no active Pomodoro session.");

    private async Task ExecuteAndRefreshAsync(
        Func<CancellationToken, Task<PomodoroSession>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            _current = await unitOfWork.ExecuteAsync(operation, cancellationToken);
        }
        catch
        {
            _current = await pomodoroStore.GetActiveAsync(CancellationToken.None);
            throw;
        }
    }
}
