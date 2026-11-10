namespace AgroPlan.Domain.Workforce;

public enum TaskReviewAction { Verified = 1, Rejected = 2, CorrectionRequested = 3 }

public sealed class TaskReviewDecision
{
    private TaskReviewDecision() { }

    public TaskReviewDecision(Guid id, Guid farmId, Guid taskId, Guid? submissionId, string reviewerUserId,
        TaskReviewAction action, string? reason, DateTimeOffset decidedAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty || taskId == Guid.Empty || submissionId == Guid.Empty)
            throw new ArgumentException("Review, farm and task ids are required; a supplied submission id must be valid.");
        if (string.IsNullOrWhiteSpace(reviewerUserId) || reviewerUserId.Length > 450)
            throw new ArgumentException("Reviewer is required.", nameof(reviewerUserId));
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        if (action != TaskReviewAction.Verified && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required when rejecting or requesting correction.", nameof(reason));
        if (reason?.Trim().Length > 1000) throw new ArgumentException("Review reason is too long.", nameof(reason));
        if (decidedAt == default) throw new ArgumentException("Decision time is required.", nameof(decidedAt));
        Id = id; FarmId = farmId; WorkTaskId = taskId; SubmissionId = submissionId;
        ReviewerUserId = reviewerUserId.Trim(); Action = action;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(); DecidedAt = decidedAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid WorkTaskId { get; private set; }
    public Guid? SubmissionId { get; private set; }
    public string ReviewerUserId { get; private set; } = string.Empty;
    public TaskReviewAction Action { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
}
