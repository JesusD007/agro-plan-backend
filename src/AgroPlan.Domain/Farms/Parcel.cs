namespace AgroPlan.Domain.Farms;

public sealed class Parcel
{
    private Parcel() { }

    public Parcel(Guid id, Guid farmId, string name, string? location, decimal areaHectares, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Parcel id is required.", nameof(id));
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parcel name is required.", nameof(name));
        if (areaHectares <= 0 || areaHectares > 1_000_000) throw new ArgumentOutOfRangeException(nameof(areaHectares));

        Id = id;
        FarmId = farmId;
        Name = name.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        AreaHectares = areaHectares;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid FarmId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public decimal AreaHectares { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Farm Farm { get; private set; } = null!;

    public void Update(string name, string? location, decimal areaHectares, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parcel name is required.", nameof(name));
        if (areaHectares <= 0 || areaHectares > 1_000_000) throw new ArgumentOutOfRangeException(nameof(areaHectares));
        Name = name.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        AreaHectares = areaHectares;
        UpdatedAt = updatedAt;
    }
}
