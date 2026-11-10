using AgroPlan.Api.Hubs;
using AgroPlan.Infrastructure.Persistence;
using AgroPlan.Domain.Farms;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Api.Services;

public sealed class JobStatusNotificationService(
    IServiceScopeFactory scopeFactory,
    IHubContext<JobStatusHub> hubContext,
    ILogger<JobStatusNotificationService> logger) : BackgroundService
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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Job status notifications will retry after an error ({ExceptionType}).", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task NotifyPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgroPlanDbContext>();
        var pending = await dbContext.SowingProposals
            .Where(proposal => proposal.NotifiedStatusVersion < proposal.StatusVersion)
            .OrderBy(proposal => proposal.StatusChangedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var proposal in pending)
        {
            var activeUserIds = await dbContext.FarmMemberships.AsNoTracking()
                .Where(membership => membership.FarmId == proposal.FarmId && membership.IsActive &&
                    (membership.Role == FarmRole.Owner || membership.Role == FarmRole.Administrator))
                .Select(membership => membership.UserId)
                .ToListAsync(cancellationToken);
            var notification = new
            {
                jobId = proposal.Id,
                farmId = proposal.FarmId,
                status = proposal.Status.ToString().ToLowerInvariant(),
                updatedAt = proposal.StatusChangedAt,
                statusVersion = proposal.StatusVersion
            };
            foreach (var userId in activeUserIds)
                await hubContext.Clients.Group(JobStatusHub.FarmUserGroup(proposal.FarmId, userId))
                    .SendAsync("jobStatusChanged", notification, cancellationToken);
            proposal.MarkStatusNotified(proposal.StatusVersion);
        }
        if (pending.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }
}
