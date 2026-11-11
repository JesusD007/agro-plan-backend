using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgroPlan.Worker.Jobs;

public sealed class SymptomPhotoCleanupService(IServiceScopeFactory scopeFactory, IFileStorage storage,
    ILogger<SymptomPhotoCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessDueDeletionsAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning("Symptom photo cleanup poll failed ({ExceptionType}).", exception.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public async Task<int> ProcessDueDeletionsAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var reports = await db.Set<SymptomReport>().Include(x => x.Photos)
            .Where(x => x.Photos.Any(photo => photo.DeleteAfter != null && photo.DeletedAt == null))
            .Take(100).ToListAsync(ct);
        var deletedCount = 0;
        foreach (var report in reports)
        foreach (var photo in report.Photos)
        {
            var now = DateTimeOffset.UtcNow;
            if (!photo.TryClaimDeletion(now, TimeSpan.FromMinutes(5))) continue;
            await db.SaveChangesAsync(ct);
            try
            {
                if (!string.IsNullOrWhiteSpace(photo.ObjectKey)) await storage.DeleteAsync(photo.ObjectKey, ct);
                photo.MarkDeleted(DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(ct);
                deletedCount++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                photo.MarkDeletionFailed(exception.GetType().Name, DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Symptom photo deletion failed; retry is scheduled ({ExceptionType}).", exception.GetType().Name);
            }
        }
        return deletedCount;
    }
}
