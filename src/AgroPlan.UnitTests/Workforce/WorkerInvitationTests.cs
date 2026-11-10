using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkerInvitationTests
{
    [Fact]
    public void Worker_role_is_part_of_the_farm_membership_contract()
    {
        Assert.True(Enum.TryParse<FarmRole>("Worker", out var role));
        Assert.Equal("Worker", role.ToString());
    }

    [Fact]
    public void Invitation_expires_after_seven_days_and_accepts_matching_verified_email_once()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = Create(now);

        Assert.Equal(now.AddDays(7), invitation.ExpiresAt);
        Assert.Equal(new string('a', 64), invitation.TokenDigest);
        invitation.Accept("worker-1", "WORKER@EXAMPLE.TEST", now.AddMinutes(1));

        Assert.Equal("worker-1", invitation.AcceptedByUserId);
        Assert.Throws<InvalidOperationException>(() => invitation.Accept("worker-1", "WORKER@EXAMPLE.TEST", now.AddMinutes(2)));
    }

    [Fact]
    public void Invitation_rejects_mismatched_email_expiry_and_cancelled_state()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = Create(now);
        Assert.Throws<InvalidOperationException>(() => invitation.Accept("worker-1", "OTHER@EXAMPLE.TEST", now));
        invitation.Cancel(now);
        Assert.Throws<InvalidOperationException>(() => invitation.Accept("worker-1", "WORKER@EXAMPLE.TEST", now));
        Assert.Throws<ArgumentException>(() => new WorkerInvitation(Guid.NewGuid(), Guid.NewGuid(),
            "worker@example.test", new string('b', 64), "owner-1", now, now.AddDays(8)));
    }

    [Fact]
    public void Renewal_replaces_digest_and_restarts_seven_day_window()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = Create(now);
        var renewedAt = now.AddDays(2);
        invitation.Renew(new string('c', 64), renewedAt);

        Assert.Equal(new string('c', 64), invitation.TokenDigest);
        Assert.Equal(renewedAt, invitation.IssuedAt);
        Assert.Equal(renewedAt.AddDays(7), invitation.ExpiresAt);
    }

    private static WorkerInvitation Create(DateTimeOffset now) => new(Guid.NewGuid(), Guid.NewGuid(),
        "worker@example.test", new string('a', 64), "owner-1", now, now.AddDays(7));
}
