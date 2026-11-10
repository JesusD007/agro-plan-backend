namespace AgroPlan.Domain.Farms;

public sealed class FarmMembershipAuditEntry
{
    private FarmMembershipAuditEntry() { }

    public FarmMembershipAuditEntry(Guid farmId, string targetUserId, string actorUserId, string action, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        FarmId = farmId;
        TargetUserId = targetUserId;
        ActorUserId = actorUserId;
        Action = action;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string TargetUserId { get; private set; } = string.Empty;
    public string ActorUserId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
}
