namespace AgroPlan.Domain.AdvisoryAccess;

/// <summary>Categories an advisor may read. The type deliberately contains no write scopes.</summary>
public enum AdvisorReportScope
{
    Costs = 1,
    Production = 2
}

/// <summary>A farm-specific, time-bounded, producer-authorized read grant.</summary>
public sealed class AdvisorAccessGrant
{
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(90);

    private AdvisorAccessGrant() { }

    public AdvisorAccessGrant(Guid id, Guid farmId, string advisorUserId, IReadOnlyCollection<AdvisorReportScope> scopes,
        string grantedByUserId, string purpose, DateTimeOffset startsAt, DateTimeOffset expiresAt, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty) throw new ArgumentException("Grant and farm ids are required.");
        if (string.IsNullOrWhiteSpace(advisorUserId)) throw new ArgumentException("Advisor is required.", nameof(advisorUserId));
        if (string.IsNullOrWhiteSpace(grantedByUserId)) throw new ArgumentException("Granting producer is required.", nameof(grantedByUserId));
        if (advisorUserId == grantedByUserId) throw new ArgumentException("A producer cannot grant advisor access to themself.", nameof(advisorUserId));
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length > 500) throw new ArgumentException("Purpose is required and must not exceed 500 characters.", nameof(purpose));
        if (scopes is null || scopes.Count == 0 || scopes.Any(scope => !Enum.IsDefined(scope)) || scopes.Distinct().Count() != scopes.Count)
            throw new ArgumentException("At least one valid, non-duplicated read scope is required.", nameof(scopes));
        if (startsAt >= expiresAt || expiresAt - startsAt > MaximumDuration)
            throw new ArgumentException("Grant validity must be positive and no longer than 90 days.", nameof(expiresAt));

        Id = id;
        FarmId = farmId;
        AdvisorUserId = advisorUserId;
        Scopes = scopes.Order().ToArray();
        GrantedByUserId = grantedByUserId;
        Purpose = purpose.Trim();
        StartsAt = startsAt;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string AdvisorUserId { get; private set; } = string.Empty;
    public AdvisorReportScope[] Scopes { get; private set; } = [];
    public string GrantedByUserId { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public bool Allows(AdvisorReportScope scope, DateTimeOffset now) =>
        RevokedAt is null && StartsAt <= now && now < ExpiresAt && Scopes.Contains(scope);

    public void RecordUse(DateTimeOffset at)
    {
        if (RevokedAt is null && at >= StartsAt && at < ExpiresAt)
            LastUsedAt = at;
    }

    public void Revoke(string actorUserId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Revoking producer is required.", nameof(actorUserId));
        if (RevokedAt.HasValue) throw new InvalidOperationException("Grant is already revoked.");
        RevokedAt = at;
        RevokedByUserId = actorUserId;
    }
}
