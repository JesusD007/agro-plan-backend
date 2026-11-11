namespace AgroPlan.Domain.Symptoms;

/// <summary>Trusted provisioning record. This feature intentionally has no self-service verification endpoint.</summary>
public sealed class TrustedAgronomistVerification
{
    private TrustedAgronomistVerification() { }

    public TrustedAgronomistVerification(string userId, string verifiedByUserId, string credentialReference, DateTimeOffset verifiedAt)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(verifiedByUserId))
            throw new ArgumentException("Verified user and trusted verifier are required.");
        if (string.IsNullOrWhiteSpace(credentialReference) || credentialReference.Length > 300)
            throw new ArgumentException("A credential reference is required.", nameof(credentialReference));
        UserId = userId;
        VerifiedByUserId = verifiedByUserId;
        CredentialReference = credentialReference;
        VerifiedAt = verifiedAt;
    }

    public string UserId { get; private set; } = string.Empty;
    public string VerifiedByUserId { get; private set; } = string.Empty;
    public string CredentialReference { get; private set; } = string.Empty;
    public DateTimeOffset VerifiedAt { get; private set; }
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
