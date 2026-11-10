using AgroPlan.Api.Hubs;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Services;

/// <summary>Delivers confirmed schedule revisions to the assigned worker and records delivery for retry safety.</summary>
public sealed class TaskScheduleNotificationConsumer(IServiceScopeFactory scopes,
    IHubContext<JobStatusHub> hub, ILogger<TaskScheduleNotificationConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NotifyPendingAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Task schedule notifications remain pending after delivery failure ({ExceptionType}).", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task NotifyPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var pending = await db.TaskScheduleRevisions.Where(x => x.Status == TaskScheduleRevisionStatus.Confirmed && x.NotifiedAt == null)
            .OrderBy(x => x.ConfirmedAt).Take(50).ToListAsync(ct);
        foreach (var revision in pending)
        {
            var workerId = await db.WorkTasks.AsNoTracking().Where(x => x.FarmId == revision.FarmId && x.Id == revision.WorkTaskId)
                .Select(x => x.AssignedWorkerUserId).SingleOrDefaultAsync(ct);
            if (workerId is null) continue;
            var activeWorker = await db.FarmMemberships.AsNoTracking().AnyAsync(x => x.FarmId == revision.FarmId &&
                x.UserId == workerId && x.IsActive && x.Role == AgroPlan.Domain.Farms.FarmRole.Worker, ct);
            if (!activeWorker) continue;
            await hub.Clients.Group(JobStatusHub.FarmUserGroup(revision.FarmId, workerId)).SendAsync("taskScheduleChanged",
                new { revisionId = revision.Id, taskId = revision.WorkTaskId, dueDate = revision.ProposedDate,
                    version = revision.TaskVersion + 1, changedAt = revision.ConfirmedAt }, ct);
            revision.MarkNotified(DateTimeOffset.UtcNow);
        }
        if (pending.Count > 0) await db.SaveChangesAsync(ct);
    }
}
