using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkerProgressTests
{
    [Fact]
    public void Rankings_default_off_and_streak_requires_an_explicit_farm_timezone()
    {
        var settings = new FarmWorkerSettings(Guid.NewGuid());
        Assert.False(settings.RankingsEnabled);
        var now = new DateTimeOffset(2026, 11, 10, 4, 0, 0, TimeSpan.Zero);
        Assert.Null(settings.GetCurrentStreak([now.AddDays(-1)], now));
        settings.SetTimeZone("America/Santo_Domingo");
        Assert.Equal(2, settings.GetCurrentStreak([now.AddDays(-1), now], now));
    }

    [Fact]
    public void Task_carries_objective_award_values_and_ledger_records_rule_version()
    {
        var task = new WorkTask(Guid.NewGuid(), Guid.NewGuid(), "worker-1", new DateOnly(2026, 11, 12),
            "Harvest row", true, null, null, null, null, pointsForVerification: 15, completeEvidenceBonusPoints: 5);
        Assert.Equal(15, task.PointsForVerification);
        Assert.Equal(5, task.CompleteEvidenceBonusPoints);

        var reward = new VerifiedWorkReward(Guid.NewGuid(), task.FarmId, task.AssignedWorkerUserId,
            VerifiedWorkRewardKind.Points, "verified-task", 20, 3, DateTimeOffset.UtcNow, task.Id);
        Assert.Equal(3, reward.RuleVersion);
        Assert.Equal(task.Id, reward.WorkTaskId);
        Assert.Equal(20, reward.Points);
    }

    [Fact]
    public void Manual_reward_requires_auditable_administrator_and_description()
    {
        Assert.Throws<ArgumentException>(() => new VerifiedWorkReward(Guid.NewGuid(), Guid.NewGuid(), "worker-1",
            VerifiedWorkRewardKind.Manual, "manual", 0, 1, DateTimeOffset.UtcNow));

        var reward = new VerifiedWorkReward(Guid.NewGuid(), Guid.NewGuid(), "worker-1", VerifiedWorkRewardKind.Manual,
            "manual", 0, 1, DateTimeOffset.UtcNow, grantedByUserId: "admin-1", description: "Safety recognition");
        Assert.Equal("admin-1", reward.GrantedByUserId);
        Assert.Equal("Safety recognition", reward.Description);
    }
}
