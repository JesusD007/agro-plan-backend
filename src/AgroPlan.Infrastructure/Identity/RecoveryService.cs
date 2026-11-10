using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgroPlan.Application.Abstractions;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgroPlan.Infrastructure.Identity;

public sealed record RecoveryRequestCreated(Guid RequestId);
public sealed record PendingRecoveryRequest(Guid Id, string Email, DateTimeOffset CreatedAt);
public sealed record RecoveryReviewResult(bool Succeeded, string? Error);
public sealed record PasswordChangeResult(bool Succeeded, IReadOnlyList<string> Errors);

public sealed class RecoveryService(
    AgroPlanDbContext dbContext,
    UserManager<ApplicationUser> users,
    IEmailSender emailSender,
    RefreshSessionService sessions,
    IConfiguration configuration,
    byte[] signingKey,
    ILogger<RecoveryService> logger)
{
    private readonly byte[] _hashKey = HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes("AgroPlan.Recovery.HashKey.v1"));

    public async Task<RecoveryRequestCreated> StartAsync(string email, string? sourceIp, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedEmail = users.NormalizeEmail(email.Trim()) ?? email.Trim().ToUpperInvariant();
        var emailHash = HashIdentifier(normalizedEmail);
        var ipHash = HashIdentifier(string.IsNullOrWhiteSpace(sourceIp) ? "unknown-client" : sourceIp);
        var requestId = Guid.NewGuid();
        var request = new RecoveryRequest
        {
            Id = requestId,
            EmailHash = emailHash,
            SourceIpHash = ipHash,
            RecoveryCodeHash = HashIdentifier(CreateCode()),
            CreatedAt = now,
            CodeExpiresAt = now,
            Status = RecoveryStatuses.NoAccount
        };

        var accountLimit = PositiveLimit("Recovery:MaximumRequestsPerAccountPerHour", 3);
        var ipLimit = PositiveLimit("Recovery:MaximumRequestsPerIpPerHour", 10);
        var cutoff = now.AddHours(-1);
        var recentRequests = dbContext.RecoveryRequests.Where(candidate => candidate.CreatedAt >= cutoff);
        var accountCount = await recentRequests.CountAsync(candidate => candidate.EmailHash == emailHash, cancellationToken);
        var ipCount = await recentRequests.CountAsync(candidate => candidate.SourceIpHash == ipHash, cancellationToken);
        if (accountCount >= accountLimit || ipCount >= ipLimit) return new RecoveryRequestCreated(Guid.NewGuid());

        var user = await users.FindByEmailAsync(email.Trim());
        string? code = null;
        if (user is not null)
        {
            code = CreateCode();
            request.UserId = user.Id;
            request.RecoveryCodeHash = HashIdentifier(code);
            request.CreatedAt = now;
            request.CodeExpiresAt = now.AddMinutes(PositiveLimit("Recovery:CodeLifetimeMinutes", 15));
            request.Status = RecoveryStatuses.CodePending;
        }

        dbContext.RecoveryRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (user is not null && code is not null)
        {
            var body = $"Your AgroPlan account recovery code is {code}. It expires in {request.CodeExpiresAt - now:mm\\:ss}. Enter it with request ID {request.Id}. If you did not request recovery, ignore this message.";
            try
            {
                await emailSender.SendAsync(user.Email ?? email.Trim(), "AgroPlan account recovery code", body, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning("Could not deliver account recovery email for request {RecoveryRequestId}.", request.Id);
            }
        }

        return new RecoveryRequestCreated(request.Id);
    }

    public async Task<bool> VerifyEmailCodeAsync(Guid requestId, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.RecoveryRequests
            .FromSqlInterpolated($"SELECT * FROM identity.recovery_requests WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null || request.UserId is null || request.Status != RecoveryStatuses.CodePending ||
            request.EmailVerifiedAt is not null || request.CodeExpiresAt <= DateTimeOffset.UtcNow ||
            request.CodeAttempts >= PositiveLimit("Recovery:MaximumCodeAttempts", 5))
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        request.CodeAttempts++;
        if (!IsHashMatch(request.RecoveryCodeHash, HashIdentifier(code)))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        request.EmailVerifiedAt = DateTimeOffset.UtcNow;
        request.Status = RecoveryStatuses.PendingSupportReview;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PendingRecoveryRequest>> GetPendingAsync(CancellationToken cancellationToken)
    {
        return await (from request in dbContext.RecoveryRequests.AsNoTracking()
                      join user in dbContext.Users.AsNoTracking() on request.UserId equals user.Id
                      where request.Status == RecoveryStatuses.PendingSupportReview
                      orderby request.CreatedAt
                      select new PendingRecoveryRequest(request.Id, user.Email ?? string.Empty, request.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<RecoveryReviewResult> ReviewAsync(Guid requestId, string supportUserId, bool approve, string? note,
        CancellationToken cancellationToken)
    {
        if (note?.Length > 500) return new RecoveryReviewResult(false, "Review note must not exceed 500 characters.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.RecoveryRequests
            .FromSqlInterpolated($"SELECT * FROM identity.recovery_requests WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null || request.UserId is null || request.Status != RecoveryStatuses.PendingSupportReview)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RecoveryReviewResult(false, "Recovery request is unavailable for review.");
        }

        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
        {
            request.Status = RecoveryStatuses.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RecoveryReviewResult(false, "Recovery request is unavailable for review.");
        }

        var now = DateTimeOffset.UtcNow;
        request.ReviewedAt = now;
        request.ReviewedByUserId = supportUserId;
        request.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        request.Status = approve ? RecoveryStatuses.Approved : RecoveryStatuses.Rejected;
        string? resetToken = null;
        if (approve)
        {
            resetToken = RefreshSessionService.CreateToken();
            request.ResetTokenHash = HashIdentifier(resetToken);
            request.ResetTokenExpiresAt = now.AddMinutes(PositiveLimit("Recovery:PasswordChangeTokenLifetimeMinutes", 15));
        }

        dbContext.RecoveryAuditEntries.Add(new RecoveryAuditEntry
        {
            Id = Guid.NewGuid(),
            RecoveryRequestId = request.Id,
            SupportUserId = supportUserId,
            Action = approve ? "Approved" : "Rejected",
            Note = request.ReviewNote,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        if (approve && resetToken is not null)
        {
            var body = $"Support approved your AgroPlan recovery request. Use this one-time password change token within 15 minutes: {resetToken}. If you did not request recovery, contact AgroPlan support.";
            try
            {
                await emailSender.SendAsync(user.Email ?? string.Empty, "AgroPlan password change", body, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning("Could not deliver approved password change token for request {RecoveryRequestId}.", request.Id);
                return new RecoveryReviewResult(false, "Email delivery is unavailable; no approval was recorded.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new RecoveryReviewResult(true, null);
    }

    public async Task<PasswordChangeResult> CompletePasswordChangeAsync(Guid requestId, string token, string newPassword,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.RecoveryRequests
            .FromSqlInterpolated($"SELECT * FROM identity.recovery_requests WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (request is null || request.UserId is null || request.Status != RecoveryStatuses.Approved ||
            request.ResetTokenConsumedAt is not null || request.ResetTokenExpiresAt is null || request.ResetTokenExpiresAt <= now || request.ResetTokenHash is null ||
            !IsHashMatch(request.ResetTokenHash, HashIdentifier(token)))
        {
            await transaction.CommitAsync(cancellationToken);
            return new PasswordChangeResult(false, ["The recovery token is invalid or expired."]);
        }

        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PasswordChangeResult(false, ["The recovery token is invalid or expired."]);
        }

        var identityToken = await users.GeneratePasswordResetTokenAsync(user);
        var resetResult = await users.ResetPasswordAsync(user, identityToken, newPassword);
        if (!resetResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PasswordChangeResult(false, resetResult.Errors.Select(error => error.Description).ToArray());
        }

        request.Status = RecoveryStatuses.Completed;
        request.ResetTokenConsumedAt = now;
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordChangeResult(true, []);
    }

    private int PositiveLimit(string key, int defaultValue)
    {
        var value = configuration.GetValue<int?>(key) ?? defaultValue;
        return value is > 0 and <= 100_000 ? value : defaultValue;
    }

    private string HashIdentifier(string value) => Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes(value)));

    private bool IsHashMatch(string expectedHex, string actualHex)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expectedHex), Convert.FromHexString(actualHex));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string CreateCode() => RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8", CultureInfo.InvariantCulture);
}
