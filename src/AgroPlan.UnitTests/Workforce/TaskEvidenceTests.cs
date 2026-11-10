using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class TaskEvidenceTests
{
    [Fact]
    public void Task_evidence_submission_aggregate_is_available()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Create(Guid.NewGuid(), 1, null, now);
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), 1, null, now, location: "farm gate"));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), 1, null, now, capturedAt: now));

        first.Finalize("private/farm/task/final", 512, now.AddMinutes(1));
        Assert.Equal("private/farm/task/final", first.ObjectKey);
        Assert.Equal(512, first.SizeBytes);
        first.Finalize("private/other", 512, now.AddMinutes(1));
        Assert.Equal("private/farm/task/final", first.ObjectKey);

        var correction = Create(Guid.NewGuid(), 2, first.Id, now.AddMinutes(2), consentedToLocation: true,
            location: "Parcel 1", consentedToCapturedAt: true, capturedAt: now);
        Assert.Equal(first.Id, correction.PreviousSubmissionId);
        Assert.Equal("Parcel 1", correction.Location);
        Assert.Equal(now, correction.CapturedAt);
    }

    [Fact]
    public void Evidence_upload_enforces_private_file_limits_and_authorization_expiry()
    {
        var now = DateTimeOffset.UtcNow;
        var upload = Create(Guid.NewGuid(), 1, null, now);
        Assert.Throws<InvalidOperationException>(() => upload.Finalize("private/object", 511, now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => upload.Finalize("private/object", 512, now.AddMinutes(11)));
    }

    private static TaskEvidenceSubmission Create(Guid id, int revision, Guid? previousId, DateTimeOffset now,
        bool consentedToLocation = false, string? location = null, bool consentedToCapturedAt = false,
        DateTimeOffset? capturedAt = null) => new(id, Guid.NewGuid(), Guid.NewGuid(), "worker-1", revision,
        previousId, Guid.NewGuid(), "private/staging/token", "image/jpeg", 512, consentedToLocation, location,
        consentedToCapturedAt, capturedAt, now, now.AddMinutes(10));
}
