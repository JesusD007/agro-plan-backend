namespace AgroPlan.Domain.Workforce;

public enum VerifiedWorkRewardKind { Points = 1, Badge = 2, Manual = 3 }

/// <summary>Append-only ledger row; manual rewards record recognition and never represent payment.</summary>
public sealed class VerifiedWorkReward
{
    private VerifiedWorkReward() { }

    public VerifiedWorkReward(Guid id, Guid farmId, string workerUserId, VerifiedWorkRewardKind kind,
        string ruleKey, int points, int ruleVersion, DateTimeOffset awardedAt, Guid? workTaskId = null,
        string? grantedByUserId = null, string? description = null)
    {
        if (id == Guid.Empty || farmId == Guid.Empty) throw new ArgumentException("Reward and farm ids are required.");
        if (string.IsNullOrWhiteSpace(workerUserId) || workerUserId.Length > 450)
            throw new ArgumentException("Worker is required.", nameof(workerUserId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(ruleKey) || ruleKey.Trim().Length > 100)
            throw new ArgumentException("Reward rule key is required and limited to 100 characters.", nameof(ruleKey));
        if (points is < 0 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(points));
        if (ruleVersion < 1) throw new ArgumentOutOfRangeException(nameof(ruleVersion));
        if (awardedAt == default) throw new ArgumentException("Reward timestamp is required.", nameof(awardedAt));
        if (kind == VerifiedWorkRewardKind.Manual && (string.IsNullOrWhiteSpace(grantedByUserId) || string.IsNullOrWhiteSpace(description)))
            throw new ArgumentException("Manual rewards require an administrator and a description.");
        if (grantedByUserId?.Length > 450 || description?.Trim().Length > 1000)
            throw new ArgumentException("Reward audit data exceeds its size limit.");

        Id = id; FarmId = farmId; WorkerUserId = workerUserId.Trim(); Kind = kind; RuleKey = ruleKey.Trim();
        Points = points; RuleVersion = ruleVersion; AwardedAt = awardedAt; WorkTaskId = workTaskId;
        GrantedByUserId = grantedByUserId?.Trim(); Description = description?.Trim();
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string WorkerUserId { get; private set; } = string.Empty;
    public Guid? WorkTaskId { get; private set; }
    public VerifiedWorkRewardKind Kind { get; private set; }
    public string RuleKey { get; private set; } = string.Empty;
    public int Points { get; private set; }
    public int RuleVersion { get; private set; }
    public DateTimeOffset AwardedAt { get; private set; }
    public string? GrantedByUserId { get; private set; }
    public string? Description { get; private set; }
}
