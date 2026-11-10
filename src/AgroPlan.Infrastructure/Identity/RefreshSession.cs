namespace AgroPlan.Infrastructure.Identity;

public sealed class RefreshSession
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
}

public sealed class RecoveryRequest
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string EmailHash { get; set; } = string.Empty;
    public string SourceIpHash { get; set; } = string.Empty;
    public string RecoveryCodeHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset CodeExpiresAt { get; set; }
    public int CodeAttempts { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string Status { get; set; } = RecoveryStatuses.CodePending;
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
    public string? ResetTokenHash { get; set; }
    public DateTimeOffset? ResetTokenExpiresAt { get; set; }
    public DateTimeOffset? ResetTokenConsumedAt { get; set; }
}

public sealed class RecoveryAuditEntry
{
    public Guid Id { get; set; }
    public Guid RecoveryRequestId { get; set; }
    public string SupportUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class RecoveryStatuses
{
    public const string CodePending = "CodePending";
    public const string NoAccount = "NoAccount";
    public const string PendingSupportReview = "PendingSupportReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Completed = "Completed";
    public const string Expired = "Expired";
}

public static class SupportRole
{
    public const string Name = "Support";
}
