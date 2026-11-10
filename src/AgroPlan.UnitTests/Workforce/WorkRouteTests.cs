using AgroPlan.Application.Abstractions;
using Xunit;

namespace AgroPlan.UnitTests.Workforce;

public sealed class WorkRouteTests
{
    [Fact]
    public void Provider_neutral_route_contract_and_unconfigured_provider_are_available()
    {
        var applicationAssembly = typeof(IClimateProvider).Assembly;
        var infrastructureAssembly = typeof(AgroPlan.Infrastructure.Persistence.AgroPlanDbContext).Assembly;

        Assert.NotNull(applicationAssembly.GetType("AgroPlan.Application.Abstractions.IWorkRouteProvider"));
        Assert.NotNull(infrastructureAssembly.GetType("AgroPlan.Infrastructure.Routing.UnconfiguredWorkRouteProvider"));
    }
}
