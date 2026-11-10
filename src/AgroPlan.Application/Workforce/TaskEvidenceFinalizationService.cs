using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Workforce;

namespace AgroPlan.Application.Workforce;

public sealed class TaskEvidenceFinalizationService(ITaskEvidenceFinalizationStore store, IFileStorage storage)
{
    public async Task<TaskEvidenceSubmission?> FinalizeAsync(Guid farmId, Guid taskId, Guid submissionId,
        string workerUserId, CancellationToken ct)
    {
        return await store.FinalizeAsync(farmId, taskId, submissionId, workerUserId, async (submission, token) =>
        {
            var now = DateTimeOffset.UtcNow;
            if (now > submission.ExpiresAt) throw new InvalidOperationException("Evidence upload authorization has expired.");
            var staged = await storage.GetMetadataAsync(submission.UploadObjectKey, token);
            if (staged is null) throw new InvalidOperationException("The uploaded evidence object was not found.");
            if (staged.ContentType != submission.ContentType || staged.SizeBytes != submission.ExpectedSizeBytes ||
                staged.SizeBytes is <= 0 or > 10 * 1024 * 1024)
                throw new InvalidOperationException("Uploaded evidence metadata does not match its authorization.");
            var finalObjectKey = $"farms/{farmId:D}/work-tasks/{taskId:D}/evidence/{submissionId:D}/final";
            var final = await storage.FinalizeUploadAsync(submission.UploadObjectKey, finalObjectKey, token);
            if (final is null || final.Metadata.ContentType != submission.ContentType ||
                final.Metadata.SizeBytes != submission.ExpectedSizeBytes || final.Metadata.SizeBytes is <= 0 or > 10 * 1024 * 1024)
                throw new InvalidOperationException("Uploaded evidence could not be finalized with matching metadata.");
            submission.Finalize(final.ObjectKey, final.Metadata.SizeBytes, now);
        }, ct);
    }
}
