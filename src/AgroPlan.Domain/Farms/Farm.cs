namespace AgroPlan.Domain.Farms;

public sealed class Farm
{
    private Farm() { }

    public Farm(Guid id, string name, string? location, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Farm name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ICollection<FarmMembership> Memberships { get; private set; } = new List<FarmMembership>();
}

public enum FarmRole
{
    Owner = 1,
    Administrator = 2
}

public sealed class FarmMembership
{
    private FarmMembership() { }

    public FarmMembership(Guid farmId, string userId, FarmRole role, DateTimeOffset createdAt)
    {
        if (farmId == Guid.Empty) throw new ArgumentException("Farm id is required.", nameof(farmId));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("User id is required.", nameof(userId));

        FarmId = farmId;
        UserId = userId;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid FarmId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public FarmRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Farm Farm { get; private set; } = null!;
}
