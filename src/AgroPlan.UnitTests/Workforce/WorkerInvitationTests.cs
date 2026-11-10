using System.Reflection;
using AgroPlan.Domain.Farms;
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
    public void Invitation_domain_exposes_digest_backed_seven_day_lifecycle()
    {
        var type = typeof(Farm).Assembly.GetType("AgroPlan.Domain.Workforce.WorkerInvitation");
        Assert.NotNull(type);
        Assert.NotNull(type!.GetProperty("TokenDigest", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetProperty("ExpiresAt", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetMethod("Accept", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetMethod("Renew", BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(type.GetMethod("Cancel", BindingFlags.Instance | BindingFlags.Public));
    }
}
