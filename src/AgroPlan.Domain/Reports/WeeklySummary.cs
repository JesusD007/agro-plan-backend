namespace AgroPlan.Domain.Reports;

public enum WeeklySummaryNarrativeStatus
{
    Unavailable = 0,
    Pending = 1,
    Available = 2,
    Failed = 3
}

/// <summary>A farm-scoped, request-time snapshot. Prose and sharing remain separate from its deterministic projection.</summary>
public sealed class WeeklySummary
{
    private WeeklySummary() { }

    public WeeklySummary(Guid id, Guid farmId, string requestedByUserId, DateOnly requestedWeekAnchorDate,
        DateOnly weekStartDate, DateOnly weekEndDate, Guid? cropCycleId, string timeZoneId,
        string projectionJson, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || farmId == Guid.Empty) throw new ArgumentException("Summary and farm ids are required.");
        if (string.IsNullOrWhiteSpace(requestedByUserId)) throw new ArgumentException("Requester is required.", nameof(requestedByUserId));
        if (requestedWeekAnchorDate == default || weekStartDate == default || weekEndDate == default || weekEndDate < weekStartDate)
            throw new ArgumentException("A valid weekly period is required.");
        if (cropCycleId == Guid.Empty) throw new ArgumentException("Crop cycle id cannot be empty.", nameof(cropCycleId));
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 100)
            throw new ArgumentException("A farm time zone is required.", nameof(timeZoneId));
        if (string.IsNullOrWhiteSpace(projectionJson) || projectionJson.Length > 1_000_000)
            throw new ArgumentException("A bounded deterministic projection is required.", nameof(projectionJson));
        if (createdAt == default) throw new ArgumentException("Creation time is required.", nameof(createdAt));

        Id = id;
        FarmId = farmId;
        RequestedByUserId = requestedByUserId.Trim();
        RequestedWeekAnchorDate = requestedWeekAnchorDate;
        WeekStartDate = weekStartDate;
        WeekEndDate = weekEndDate;
        CropCycleId = cropCycleId;
        TimeZoneId = timeZoneId.Trim();
        ProjectionJson = projectionJson;
        CreatedAt = createdAt;
        NarrativeStatus = WeeklySummaryNarrativeStatus.Unavailable;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateOnly RequestedWeekAnchorDate { get; private set; }
    public DateOnly WeekStartDate { get; private set; }
    public DateOnly WeekEndDate { get; private set; }
    public Guid? CropCycleId { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public string ProjectionJson { get; private set; } = string.Empty;
    public WeeklySummaryNarrativeStatus NarrativeStatus { get; private set; }
    public string? NarrativeText { get; private set; }
    public Guid[] NarrativeSourceIds { get; private set; } = [];
    public string? SharedAdvisorUserId { get; private set; }
    public string? SharedByUserId { get; private set; }
    public DateTimeOffset? SharedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int Version { get; private set; }

    public void MarkNarrativePending()
    {
        NarrativeStatus = WeeklySummaryNarrativeStatus.Pending;
        NarrativeText = null;
        Version++;
    }

    public void SetNarrative(string? text, WeeklySummaryNarrativeStatus status, IReadOnlyCollection<Guid>? sourceIds = null)
    {
        if (status is not (WeeklySummaryNarrativeStatus.Available or WeeklySummaryNarrativeStatus.Unavailable or WeeklySummaryNarrativeStatus.Failed))
            throw new ArgumentOutOfRangeException(nameof(status), "Narrative can only be finalized to a terminal status.");
        if (status == WeeklySummaryNarrativeStatus.Available && string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Available narrative requires text.", nameof(text));
        if (status != WeeklySummaryNarrativeStatus.Available && text is not null)
            throw new ArgumentException("Unavailable narrative cannot contain text.", nameof(text));
        if (text?.Length > 16_384) throw new ArgumentException("Narrative exceeds its output limit.", nameof(text));
        NarrativeText = text;
        NarrativeSourceIds = sourceIds?.Distinct().ToArray() ?? [];
        NarrativeStatus = status;
        Version++;
    }

    public void ShareWithAdvisor(string advisorUserId, string producerUserId, DateTimeOffset sharedAt)
    {
        if (string.IsNullOrWhiteSpace(advisorUserId) || string.IsNullOrWhiteSpace(producerUserId))
            throw new ArgumentException("Advisor and producer identities are required.");
        if (advisorUserId == producerUserId) throw new ArgumentException("A producer cannot share a summary with themself.", nameof(advisorUserId));
        if (sharedAt == default) throw new ArgumentException("Share time is required.", nameof(sharedAt));
        SharedAdvisorUserId = advisorUserId;
        SharedByUserId = producerUserId;
        SharedAt = sharedAt;
        Version++;
    }
}
