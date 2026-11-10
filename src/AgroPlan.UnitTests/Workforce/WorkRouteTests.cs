using AgroPlan.Application.Abstractions;
using AgroPlan.Domain.Workforce;
using AgroPlan.Infrastructure.Routing;
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

    [Fact]
    public async Task Unconfigured_provider_reports_unavailable_without_synthetic_route_data()
    {
        var provider = new UnconfiguredWorkRouteProvider();
        var request = new WorkRouteRequest(Guid.NewGuid(),
        [
            new WorkRouteStop(Guid.NewGuid(), Guid.NewGuid(), 18.5, -69.9),
            new WorkRouteStop(Guid.NewGuid(), null, null, null)
        ]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.False(result.Available);
        Assert.Null(result.ProviderName);
        Assert.Empty(result.OrderedTaskIds);
        Assert.Null(result.GeometryGeoJson);
        Assert.False(string.IsNullOrWhiteSpace(result.UnavailableReason));
    }

    [Fact]
    public void Route_contract_carries_ordered_stop_ids_and_optional_geojson_geometry()
    {
        var firstTask = Guid.NewGuid();
        var secondTask = Guid.NewGuid();
        var geometry = "{\"type\":\"LineString\",\"coordinates\":[[-69.9,18.5],[-69.8,18.6]]}";
        var result = new WorkRouteProposal(true, "approved-provider", [firstTask, secondTask], geometry, null);

        Assert.Equal([firstTask, secondTask], result.OrderedTaskIds);
        Assert.Equal(geometry, result.GeometryGeoJson);
    }

    [Fact]
    public void Versioned_work_route_snapshot_domain_type_is_available()
    {
        var domainAssembly = typeof(WorkTask).Assembly;

        Assert.NotNull(domainAssembly.GetType("AgroPlan.Domain.Workforce.WorkRoute"));
    }

    [Fact]
    public void Route_requires_administrator_confirmation_and_becomes_stale_immutably()
    {
        var taskId = Guid.NewGuid();
        var route = new WorkRoute(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "worker-1", 1,
            [taskId], [new WorkRouteTaskSource(taskId, 1, null, null)], null, DateTimeOffset.UtcNow);
        var confirmedAt = DateTimeOffset.UtcNow;

        Assert.Equal(WorkRouteStatus.Proposed, route.Status);
        route.Confirm("admin-1", confirmedAt);
        Assert.Equal(WorkRouteStatus.Confirmed, route.Status);
        Assert.Equal("admin-1", route.ConfirmedByUserId);
        Assert.Equal(confirmedAt, route.ConfirmedAt);
        Assert.True(route.MarkStale(confirmedAt.AddMinutes(1)));
        Assert.False(route.MarkStale(confirmedAt.AddMinutes(2)));
        Assert.Equal(WorkRouteStatus.Stale, route.Status);
        Assert.Equal([taskId], route.GetOrderedTaskIds());
    }

    [Theory]
    [InlineData("{\"type\":\"LineString\",\"coordinates\":[[181,10],[1,2]]}")]
    [InlineData("{\"type\":\"LineString\",\"coordinates\":[[1,2]]}")]
    [InlineData("not-geojson")]
    public void Route_rejects_invalid_or_out_of_bounds_provider_geometry(string geometry)
    {
        var taskId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new WorkRoute(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "worker-1", 1,
            [taskId], [new WorkRouteTaskSource(taskId, 1, null, null)], geometry, DateTimeOffset.UtcNow));
    }
}
