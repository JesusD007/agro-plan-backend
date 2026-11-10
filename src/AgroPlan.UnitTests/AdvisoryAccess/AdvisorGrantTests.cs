using AgroPlan.Domain.AdvisoryAccess;
using Xunit;

namespace AgroPlan.UnitTests.AdvisoryAccess;

public sealed class AdvisorGrantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Grant_is_read_only_scoped_and_bounded()
    {
        var grant = Create([AdvisorReportScope.Costs]);

        Assert.True(grant.Allows(AdvisorReportScope.Costs, Now));
        Assert.False(grant.Allows(AdvisorReportScope.Production, Now));
        Assert.Equal("producer", grant.GrantedByUserId);
        Assert.Equal("Crop cost review", grant.Purpose);
        Assert.Equal(Now.AddDays(30), grant.ExpiresAt);
    }

    [Fact]
    public void Grant_rejects_missing_scope_self_grant_and_unbounded_validity()
    {
        Assert.Throws<ArgumentException>(() => Create([]));
        Assert.Throws<ArgumentException>(() => new AdvisorAccessGrant(Guid.NewGuid(), Guid.NewGuid(), "producer",
            [AdvisorReportScope.Costs], "producer", "Purpose", Now, Now.AddDays(1), Now));
        Assert.Throws<ArgumentException>(() => new AdvisorAccessGrant(Guid.NewGuid(), Guid.NewGuid(), "advisor",
            [AdvisorReportScope.Costs], "producer", "Purpose", Now, Now.AddDays(91), Now));
    }

    [Fact]
    public void Revoke_takes_effect_immediately_and_records_actor()
    {
        var grant = Create([AdvisorReportScope.Costs, AdvisorReportScope.Production]);
        grant.RecordUse(Now);
        grant.Revoke("producer", Now.AddMinutes(1));

        Assert.False(grant.Allows(AdvisorReportScope.Costs, Now.AddMinutes(2)));
        Assert.Equal("producer", grant.RevokedByUserId);
        Assert.Equal(Now.AddMinutes(1), grant.RevokedAt);
        Assert.Equal(Now, grant.LastUsedAt);
        Assert.Throws<InvalidOperationException>(() => grant.Revoke("producer", Now.AddMinutes(3)));
    }

    [Fact]
    public void Grant_is_not_active_before_start_or_after_expiry()
    {
        var grant = Create([AdvisorReportScope.Costs]);
        Assert.False(grant.Allows(AdvisorReportScope.Costs, Now.AddTicks(-1)));
        Assert.False(grant.Allows(AdvisorReportScope.Costs, grant.ExpiresAt));
    }

    private static AdvisorAccessGrant Create(IReadOnlyCollection<AdvisorReportScope> scopes) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "advisor", scopes, "producer", "Crop cost review",
            Now, Now.AddDays(30), Now);
}
