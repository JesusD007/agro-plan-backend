namespace AgroPlan.Domain.Symptoms;

public sealed class FarmAgronomistAuthorization
{
    private FarmAgronomistAuthorization() { }

    public FarmAgronomistAuthorization(Guid farmId, string agronomistUserId, string grantedByUserId, DateTimeOffset grantedAt)
    {
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (string.IsNullOrWhiteSpace(agronomistUserId) || string.IsNullOrWhiteSpace(grantedByUserId))
            throw new ArgumentException("Agronomist and granting user are required.");
        FarmId = farmId;
        AgronomistUserId = agronomistUserId;
        GrantedByUserId = grantedByUserId;
        GrantedAt = grantedAt;
    }

    public Guid FarmId { get; private set; }
    public string AgronomistUserId { get; private set; } = string.Empty;
    public string GrantedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; private set; }
    public string? RevokedByUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive => RevokedAt is null;

    public void Revoke(string actorUserId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("Revoking user is required.", nameof(actorUserId));
        RevokedByUserId ??= actorUserId;
        RevokedAt ??= at;
    }
}
