namespace AgroPlan.Domain.Workforce;

/// <summary>Farm-scoped, administrator-controlled worker progress visibility and rules.</summary>
public sealed class FarmWorkerSettings
{
    private FarmWorkerSettings() { }

    public FarmWorkerSettings(Guid farmId)
    {
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        FarmId = farmId;
        RankingsEnabled = false;
        BadgeThresholdPoints = 100;
        TeamGoalTargetPoints = 1000;
    }

    public Guid FarmId { get; private set; }
    public bool RankingsEnabled { get; private set; }
    public string? TimeZoneId { get; private set; }
    public int BadgeThresholdPoints { get; private set; }
    public int TeamGoalTargetPoints { get; private set; }
    public int RuleVersion { get; private set; } = 1;

    public void SetRankingsEnabled(bool enabled) => RankingsEnabled = enabled;

    public void SetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 100)
            throw new ArgumentException("A valid farm time zone is required.", nameof(timeZoneId));
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        TimeZoneId = timeZoneId.Trim();
        RuleVersion++;
    }

    public void ConfigureGoals(int badgeThresholdPoints, int teamGoalTargetPoints)
    {
        if (badgeThresholdPoints is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(badgeThresholdPoints));
        if (teamGoalTargetPoints is < 1 or > 10_000_000) throw new ArgumentOutOfRangeException(nameof(teamGoalTargetPoints));
        BadgeThresholdPoints = badgeThresholdPoints;
        TeamGoalTargetPoints = teamGoalTargetPoints;
        RuleVersion++;
    }

    public int? GetCurrentStreak(IEnumerable<DateTimeOffset> verifiedAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId)) return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var dates = verifiedAt.Select(value => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime))
            .Distinct().OrderByDescending(value => value).ToHashSet();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var day = dates.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (dates.Contains(day)) { streak++; day = day.AddDays(-1); }
        return streak;
    }
}
