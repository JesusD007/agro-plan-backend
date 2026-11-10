namespace AgroPlan.Application.Abstractions;

/// <summary>A farm-scoped set of authorized task stops with only the parcel coordinates already available.</summary>
public sealed record WorkRouteRequest(Guid FarmId, IReadOnlyList<WorkRouteStop> Stops);

public sealed record WorkRouteStop(Guid TaskId, Guid? ParcelId, double? Latitude, double? Longitude);

/// <summary>Provider output; geometry is optional GeoJSON and never includes map tiles or navigation instructions.</summary>
public sealed record WorkRouteProposal(
    bool Available,
    string? ProviderName,
    IReadOnlyList<Guid> OrderedTaskIds,
    string? GeometryGeoJson,
    string? UnavailableReason);

public interface IWorkRouteProvider
{
    Task<WorkRouteProposal> GenerateAsync(WorkRouteRequest request, CancellationToken cancellationToken);
}
