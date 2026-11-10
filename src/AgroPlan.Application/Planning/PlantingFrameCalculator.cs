using AgroPlan.Domain.Crops;

namespace AgroPlan.Application.Planning;

public sealed record PlantingFrameCapacity(Guid FrameworkId, string FrameworkName, decimal PlantsPerHectare,
    decimal Capacity, string SourceName, string? SourceUrl, string? Region, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo);
public sealed record PlantingFrameCalculation(IReadOnlyList<PlantingFrameCapacity> Options, IReadOnlyList<string> MissingInputs);

public sealed class PlantingFrameCalculator
{
    public PlantingFrameCalculation Calculate(decimal? usableAreaSquareMeters, bool? aisleGeometryValidated,
        IEnumerable<CropFramework> frameworks, DateTimeOffset at, string? cropRegion = null)
    {
        var missing = new List<string>();
        if (!usableAreaSquareMeters.HasValue || usableAreaSquareMeters.Value <= 0) missing.Add("parcel.usableAreaSquareMeters");
        if (aisleGeometryValidated != true) missing.Add("parcel.aisleGeometry");
        var usableFrameworks = frameworks.Where(item => item.IsUsableAt(at) &&
            (string.IsNullOrWhiteSpace(item.Region) || (!string.IsNullOrWhiteSpace(cropRegion) && string.Equals(item.Region, cropRegion, StringComparison.OrdinalIgnoreCase)))).ToArray();
        if (usableFrameworks.Length == 0) missing.Add("crop.validatedFrameRules");
        if (missing.Count > 0) return new PlantingFrameCalculation([], missing);

        var options = usableFrameworks.Select(item =>
        {
            var plantsPerSquareMeter = 1m / (item.PlantSpacingMeters!.Value * item.RowSpacingMeters!.Value);
            var plantsPerHectare = decimal.Floor(plantsPerSquareMeter * 10_000m * (1m - item.AisleAllowanceRatio!.Value));
            return new PlantingFrameCapacity(item.Id, item.Name, plantsPerHectare,
                decimal.Floor(usableAreaSquareMeters!.Value * plantsPerSquareMeter * (1m - item.AisleAllowanceRatio.Value)),
                item.SourceName!, item.SourceUrl, item.Region, item.ValidFrom, item.ValidTo);
        }).ToArray();
        return new PlantingFrameCalculation(options, []);
    }
}
