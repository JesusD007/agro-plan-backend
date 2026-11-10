using AgroPlan.Domain.Workforce;

namespace AgroPlan.Application.Workforce;

public interface ITaskEvidenceFinalizationStore
{
    Task<TaskEvidenceSubmission?> FinalizeAsync(Guid farmId, Guid taskId, Guid submissionId, string workerUserId,
        Func<TaskEvidenceSubmission, CancellationToken, Task> finalizeObject, CancellationToken cancellationToken);
}
