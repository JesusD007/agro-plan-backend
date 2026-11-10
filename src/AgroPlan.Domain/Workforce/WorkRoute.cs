using System.Text;
using System.Text.Json;

namespace AgroPlan.Domain.Workforce;

public enum WorkRouteStatus
{
    Proposed = 1,
    Confirmed = 2,
    Stale = 3
}

public sealed record WorkRouteTaskSource(Guid TaskId, int TaskVersion, Guid? ParcelId, DateTimeOffset? ParcelUpdatedAt);

/// <summary>An immutable versioned offline route snapshot; only its review/staleness lifecycle changes.</summary>
public sealed class WorkRoute
{
    private const int MaximumGeometryLength = 100_000;

    private WorkRoute() { }

    public WorkRoute(Guid id, Guid routeId, Guid farmId, string workerUserId, int version,
        IReadOnlyList<Guid> orderedTaskIds, IReadOnlyList<WorkRouteTaskSource> taskSources,
        string? geometryGeoJson, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || routeId == Guid.Empty || farmId == Guid.Empty)
            throw new ArgumentException("Route, route-version, and farm ids are required.");
        if (string.IsNullOrWhiteSpace(workerUserId) || workerUserId.Length > 450)
            throw new ArgumentException("A worker is required.", nameof(workerUserId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (orderedTaskIds is null || orderedTaskIds.Count == 0 || orderedTaskIds.Any(taskId => taskId == Guid.Empty) ||
            orderedTaskIds.Distinct().Count() != orderedTaskIds.Count)
            throw new ArgumentException("A route requires unique, non-empty ordered task ids.", nameof(orderedTaskIds));
        if (taskSources is null || taskSources.Count != orderedTaskIds.Count ||
            taskSources.Select(source => source.TaskId).Distinct().Count() != taskSources.Count ||
            !orderedTaskIds.ToHashSet().SetEquals(taskSources.Select(source => source.TaskId)))
            throw new ArgumentException("Every ordered task must have exactly one source version.", nameof(taskSources));
        if (taskSources.Any(source => source.TaskVersion <= 0 || (source.ParcelId is null) != (source.ParcelUpdatedAt is null)))
            throw new ArgumentException("Task and parcel source versions must be complete.", nameof(taskSources));
        if (createdAt == default) throw new ArgumentException("Creation time is required.", nameof(createdAt));
        ValidateGeometry(geometryGeoJson);

        Id = id;
        RouteId = routeId;
        FarmId = farmId;
        WorkerUserId = workerUserId.Trim();
        Version = version;
        OrderedTaskIdsJson = JsonSerializer.Serialize(orderedTaskIds);
        TaskSourcesJson = JsonSerializer.Serialize(taskSources);
        GeometryGeoJson = geometryGeoJson;
        CreatedAt = createdAt;
        Status = WorkRouteStatus.Proposed;
    }

    public Guid Id { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid FarmId { get; private set; }
    public string WorkerUserId { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string OrderedTaskIdsJson { get; private set; } = "[]";
    public string TaskSourcesJson { get; private set; } = "[]";
    public string? GeometryGeoJson { get; private set; }
    public WorkRouteStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? StaleAt { get; private set; }

    public IReadOnlyList<Guid> GetOrderedTaskIds() => JsonSerializer.Deserialize<Guid[]>(OrderedTaskIdsJson) ?? [];
    public IReadOnlyList<WorkRouteTaskSource> GetTaskSources() =>
        JsonSerializer.Deserialize<WorkRouteTaskSource[]>(TaskSourcesJson) ?? [];

    public void Confirm(string administratorUserId, DateTimeOffset confirmedAt)
    {
        if (Status != WorkRouteStatus.Proposed) throw new InvalidOperationException("Only a proposed route can be confirmed.");
        if (string.IsNullOrWhiteSpace(administratorUserId) || administratorUserId.Length > 450)
            throw new ArgumentException("An administrator is required to confirm a route.", nameof(administratorUserId));
        if (confirmedAt == default) throw new ArgumentException("Confirmation time is required.", nameof(confirmedAt));
        ConfirmedByUserId = administratorUserId.Trim();
        ConfirmedAt = confirmedAt;
        Status = WorkRouteStatus.Confirmed;
    }

    public bool MarkStale(DateTimeOffset staleAt)
    {
        if (Status == WorkRouteStatus.Stale) return false;
        if (Status is not (WorkRouteStatus.Confirmed or WorkRouteStatus.Proposed)) return false;
        if (staleAt == default) throw new ArgumentException("Stale time is required.", nameof(staleAt));
        StaleAt = staleAt;
        Status = WorkRouteStatus.Stale;
        return true;
    }

    private static void ValidateGeometry(string? geometry)
    {
        if (geometry is null) return;
        if (Encoding.UTF8.GetByteCount(geometry) is 0 or > MaximumGeometryLength)
            throw new ArgumentException("Route geometry must be non-empty and at most 100 KB.", nameof(geometry));
        try
        {
            using var document = JsonDocument.Parse(geometry);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "LineString" ||
                !root.TryGetProperty("coordinates", out var coordinates) || coordinates.ValueKind != JsonValueKind.Array ||
                coordinates.GetArrayLength() < 2)
                throw new ArgumentException("Route geometry must be a GeoJSON LineString with at least two coordinates.", nameof(geometry));

            foreach (var coordinate in coordinates.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
                    throw new ArgumentException("Each route coordinate must contain longitude and latitude.", nameof(geometry));
                if (coordinate[0].ValueKind != JsonValueKind.Number || coordinate[1].ValueKind != JsonValueKind.Number)
                    throw new ArgumentException("Route longitude and latitude must be numeric.", nameof(geometry));
                var longitude = coordinate[0].GetDouble();
                var latitude = coordinate[1].GetDouble();
                if (!double.IsFinite(longitude) || !double.IsFinite(latitude) || longitude is < -180 or > 180 || latitude is < -90 or > 90)
                    throw new ArgumentException("Route coordinates are outside valid geographic bounds.", nameof(geometry));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Route geometry must be valid GeoJSON.", nameof(geometry), exception);
        }
    }
}
