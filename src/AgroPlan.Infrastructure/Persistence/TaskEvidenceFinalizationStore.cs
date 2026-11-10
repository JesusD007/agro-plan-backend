using AgroPlan.Application.Workforce;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Infrastructure.Persistence;

public sealed class TaskEvidenceFinalizationStore(AgroPlanDbContext db) : ITaskEvidenceFinalizationStore
{
    public async Task<TaskEvidenceSubmission?> FinalizeAsync(Guid farmId, Guid taskId, Guid submissionId,
        string workerUserId, Func<TaskEvidenceSubmission, CancellationToken, Task> finalizeObject,
        CancellationToken cancellationToken)
    {
        var isAssigned = await db.WorkTasks.AsNoTracking().AnyAsync(task => task.FarmId == farmId && task.Id == taskId &&
            task.AssignedWorkerUserId == workerUserId && db.FarmMemberships.Any(member => member.FarmId == farmId &&
                member.UserId == workerUserId && member.IsActive && member.Role == FarmRole.Worker), cancellationToken);
        if (!isAssigned) return null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.work_tasks WHERE \"FarmId\" = {farmId} AND \"Id\" = {taskId} FOR UPDATE", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM agro.task_evidence_submissions WHERE \"FarmId\" = {farmId} AND \"WorkTaskId\" = {taskId} AND \"Id\" = {submissionId} FOR UPDATE", cancellationToken);
        var submission = await db.TaskEvidenceSubmissions.SingleOrDefaultAsync(row => row.FarmId == farmId &&
            row.WorkTaskId == taskId && row.Id == submissionId && row.WorkerUserId == workerUserId, cancellationToken);
        if (submission is null) return null;
        if (submission.FinalizedAt.HasValue) return submission;
        var task = await db.WorkTasks.SingleAsync(row => row.FarmId == farmId && row.Id == taskId, cancellationToken);
        var activeWorker = task.AssignedWorkerUserId == workerUserId && await db.FarmMemberships.AnyAsync(member =>
            member.FarmId == farmId && member.UserId == workerUserId && member.IsActive && member.Role == FarmRole.Worker,
            cancellationToken);
        if (!activeWorker) return null;
        if (task.Status is not (WorkTaskStatus.InProgress or WorkTaskStatus.CorrectionRequested))
            throw new InvalidOperationException("Evidence cannot be finalized unless the task is in progress or awaiting correction.");
        await finalizeObject(submission, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return submission;
    }
}
