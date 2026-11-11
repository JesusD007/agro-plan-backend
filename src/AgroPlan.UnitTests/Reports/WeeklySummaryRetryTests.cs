using AgroPlan.Domain.Reports;
using Xunit;

namespace AgroPlan.UnitTests.Reports;

public sealed class WeeklySummaryRetryTests
{
    [Fact]
    public void Narrative_attempts_are_bounded_and_release_allows_retry_until_limit()
    {
        var summary = CreatePendingSummary();
        var now = DateTimeOffset.UtcNow;

        Assert.True(summary.TryStartNarrative(now, maximumAttempts: 2));
        Assert.Equal(1, summary.NarrativeAttempts);
        Assert.True(summary.ReleaseNarrativeAttempt(now));
        Assert.True(summary.TryStartNarrative(now.AddSeconds(1), maximumAttempts: 2));
        Assert.Equal(2, summary.NarrativeAttempts);
        Assert.True(summary.ReleaseNarrativeAttempt(now.AddSeconds(1)));
        Assert.False(summary.TryStartNarrative(now.AddSeconds(2), maximumAttempts: 2));
        Assert.Equal(WeeklySummaryNarrativeStatus.Pending, summary.NarrativeStatus);
    }

    [Fact]
    public void Retry_lease_blocks_duplicate_delivery_until_stale()
    {
        var summary = CreatePendingSummary();
        var now = DateTimeOffset.UtcNow;

        Assert.True(summary.TryStartNarrative(now, maximumAttempts: 3));
        Assert.False(summary.TryStartNarrative(now.AddMinutes(1), maximumAttempts: 3));
        Assert.True(summary.TryStartNarrative(now.AddMinutes(16), maximumAttempts: 3));
        Assert.Equal(2, summary.NarrativeAttempts);
    }

    [Fact]
    public void Broker_redelivery_can_recover_an_abandoned_active_lease()
    {
        var summary = CreatePendingSummary();
        var now = DateTimeOffset.UtcNow;

        Assert.True(summary.TryStartNarrative(now, maximumAttempts: 3));
        Assert.False(summary.TryStartNarrative(now.AddMinutes(1), maximumAttempts: 3));
        Assert.True(summary.TryStartNarrative(now.AddMinutes(1), maximumAttempts: 3, allowActiveLeaseTakeover: true));
        Assert.Equal(2, summary.NarrativeAttempts);
    }

    private static WeeklySummary CreatePendingSummary()
    {
        var anchor = new DateOnly(2026, 11, 9);
        var summary = new WeeklySummary(Guid.NewGuid(), Guid.NewGuid(), "producer", anchor, anchor, anchor.AddDays(6),
            null, "America/Santo_Domingo", "{}", DateTimeOffset.UtcNow);
        summary.MarkNarrativePending();
        return summary;
    }
}
