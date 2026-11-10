namespace AgroPlan.Domain.Workforce;

public sealed class WorkerInvitation
{
    private WorkerInvitation() { }

    public WorkerInvitation(Guid id, Guid farmId, string email, string tokenDigest, string issuedByUserId,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invitation id is required.", nameof(id));
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Invitee email is required.", nameof(email));
        ValidateDigest(tokenDigest);
        if (string.IsNullOrWhiteSpace(issuedByUserId)) throw new ArgumentException("Issuer id is required.", nameof(issuedByUserId));
        if (expiresAt != issuedAt.AddDays(7)) throw new ArgumentException("Worker invitations expire after seven days.", nameof(expiresAt));

        Id = id;
        FarmId = farmId;
        Email = email.Trim().ToUpperInvariant();
        TokenDigest = tokenDigest;
        IssuedByUserId = issuedByUserId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string TokenDigest { get; private set; } = string.Empty;
    public string IssuedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public string? AcceptedByUserId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    public bool IsPending(DateTimeOffset now) => AcceptedAt is null && CancelledAt is null && ExpiresAt > now;

    public void Accept(string userId, string normalizedEmail, DateTimeOffset now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Invitation is no longer pending.");
        if (!string.Equals(Email, normalizedEmail?.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Invitation email does not match the verified account.");
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User id is required.", nameof(userId));
        AcceptedAt = now;
        AcceptedByUserId = userId;
    }

    public void Renew(string tokenDigest, DateTimeOffset now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Only a pending invitation can be renewed.");
        ValidateDigest(tokenDigest);
        TokenDigest = tokenDigest;
        IssuedAt = now;
        ExpiresAt = now.AddDays(7);
    }

    public void Cancel(DateTimeOffset now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Only a pending invitation can be cancelled.");
        CancelledAt = now;
    }

    private static void ValidateDigest(string tokenDigest)
    {
        if (tokenDigest is null || tokenDigest.Length != 64 || !tokenDigest.All(Uri.IsHexDigit))
            throw new ArgumentException("Token digest must be a 256-bit hexadecimal SHA-256 value.", nameof(tokenDigest));
    }
}
