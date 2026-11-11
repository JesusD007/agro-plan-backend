using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Voice;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgroPlan.Worker.Jobs;

public sealed class ExpiredVoiceAudioCleanupService(IServiceScopeFactory scopeFactory,
    IFileStorage storage, ILogger<ExpiredVoiceAudioCleanupService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await ProcessDueAsync(stoppingToken);
                await Task.Delay(deleted == 0 ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Voice audio cleanup polling failed ({ExceptionType}); it will retry.", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.VoiceActivityDrafts.AsNoTracking()
            .Where(draft => draft.AudioDeleteAfter <= now && draft.AudioDeletedAt == null &&
                (draft.AudioDeleteRetryAt == null || draft.AudioDeleteRetryAt <= now) &&
                (draft.AudioCleanupClaimedAt == null || draft.AudioCleanupClaimedAt < now.Subtract(ClaimLease)))
            .OrderBy(draft => draft.AudioDeleteAfter).Select(draft => new { draft.FarmId, draft.Id })
            .Take(BatchSize).ToArrayAsync(cancellationToken);

        var deletedCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claim = await TryClaimAsync(candidate.FarmId, candidate.Id, cancellationToken);
            if (claim is null) continue;
            try
            {
                await storage.DeleteAsync(claim.ObjectKey, cancellationToken);
                await MarkDeletedAsync(candidate.FarmId, candidate.Id, cancellationToken);
                deletedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("Voice audio deletion failed ({ExceptionType}); durable retry is scheduled.", exception.GetType().Name);
                await MarkRetryAsync(candidate.FarmId, candidate.Id, exception.GetType().Name,
                    claim.AttemptCount, cancellationToken);
            }
        }
        return deletedCount;
    }

    private async Task<CleanupClaim?> TryClaimAsync(Guid farmId, Guid draftId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == draftId, ct);
        if (draft is null || !draft.TryClaimAudioCleanup(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Subtract(ClaimLease)))
        {
            await transaction.CommitAsync(ct);
            return null;
        }
        var key = draft.AudioObjectKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            await transaction.CommitAsync(ct);
            return null;
        }
        var claim = new CleanupClaim(key, draft.AudioDeleteAttemptCount);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return claim;
    }

    private async Task MarkDeletedAsync(Guid farmId, Guid draftId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == draftId, ct);
        draft?.MarkAudioDeleted(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task MarkRetryAsync(Guid farmId, Guid draftId, string errorCode, int attempt, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM agro.voice_activity_drafts WHERE \"FarmId\" = {farmId} AND \"Id\" = {draftId} FOR UPDATE", ct);
        var draft = await db.VoiceActivityDrafts.SingleOrDefaultAsync(item => item.FarmId == farmId && item.Id == draftId, ct);
        if (draft is not null)
        {
            var now = DateTimeOffset.UtcNow;
            var retrySeconds = Math.Min(3600, 10 * Math.Pow(2, Math.Max(0, attempt - 1)));
            draft.MarkAudioDeleteFailed(errorCode, now.AddSeconds(retrySeconds), now);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private sealed record CleanupClaim(string ObjectKey, int AttemptCount);
}
