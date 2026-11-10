using System.Security.Cryptography;
using System.Text;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Infrastructure.Identity;

public sealed record RefreshSessionIssue(string Token, DateTimeOffset ExpiresAt);
public sealed record RefreshSessionRotation(string UserId, string Token, DateTimeOffset ExpiresAt);

public sealed class RefreshSessionService(AgroPlanDbContext dbContext)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public async Task<RefreshSessionIssue> CreateAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var token = CreateToken();
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.Add(SessionLifetime)
        };
        dbContext.RefreshSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshSessionIssue(token, session.ExpiresAt);
    }

    public async Task<RefreshSessionRotation?> RotateAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var current = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            session => session.TokenHash == tokenHash, cancellationToken);
        if (current is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (current.ConsumedAt is not null || current.RevokedAt is not null)
        {
            // Reuse of a previously consumed token indicates theft; invalidate its whole family.
            await dbContext.RefreshSessions.Where(session => session.FamilyId == current.FamilyId && session.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current.ExpiresAt <= now)
        {
            current.RevokedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var replacementToken = CreateToken();
        var replacement = new RefreshSession
        {
            Id = Guid.NewGuid(),
            FamilyId = current.FamilyId,
            UserId = current.UserId,
            TokenHash = HashToken(replacementToken),
            CreatedAt = now,
            ExpiresAt = current.ExpiresAt
        };
        dbContext.RefreshSessions.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var consumed = await dbContext.RefreshSessions
            .Where(session => session.Id == current.Id && session.ConsumedAt == null && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(session => session.ConsumedAt, now)
                .SetProperty(session => session.ReplacedBySessionId, replacement.Id), cancellationToken);
        if (consumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            await dbContext.RefreshSessions.Where(session => session.FamilyId == current.FamilyId && session.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return new RefreshSessionRotation(current.UserId, replacementToken, replacement.ExpiresAt);
    }

    public async Task RevokeAsync(string token, string userId, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var now = DateTimeOffset.UtcNow;
        await dbContext.RefreshSessions
            .Where(session => session.TokenHash == tokenHash && session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await dbContext.RefreshSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, now), cancellationToken);
    }

    internal static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    internal static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
