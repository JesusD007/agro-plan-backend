using System.Reflection;
using AgroPlan.Domain.Workforce;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkerProgressTests
{
    [Fact]
    public void Farm_settings_and_verified_work_ledger_are_available()
    {
        Assert.NotNull(typeof(WorkTask).Assembly.GetType("AgroPlan.Domain.Workforce.FarmWorkerSettings"));
        Assert.NotNull(typeof(WorkTask).Assembly.GetType("AgroPlan.Domain.Workforce.VerifiedWorkReward"));
    }
}
