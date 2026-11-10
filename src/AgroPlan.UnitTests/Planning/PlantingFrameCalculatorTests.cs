using AgroPlan.Application.Planning;
using AgroPlan.Domain.Crops;
using Xunit;

namespace AgroPlan.UnitTests.Planning;

public sealed class PlantingFrameCalculatorTests
{
    private readonly PlantingFrameCalculator _calculator = new();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_with_missing_pilot_inputs_returns_missing_names_and_no_capacity()
    {
        var result = _calculator.Calculate(null, null, [], Now);

        Assert.Empty(result.Options);
        Assert.Equal(["parcel.usableAreaSquareMeters", "parcel.aisleGeometry", "crop.validatedFrameRules"], result.MissingInputs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Calculate_rejects_missing_or_nonpositive_area(decimal area)
    {
        var result = _calculator.Calculate(area, true, [ValidatedFramework()], Now);

        Assert.Empty(result.Options);
        Assert.Contains("parcel.usableAreaSquareMeters", result.MissingInputs);
    }

    [Fact]
    public void Calculate_uses_only_validated_rules_and_floors_capacity_deterministically()
    {
        // This synthetic rule is confined to the test assembly and is not a runtime agronomy value.
        var result = _calculator.Calculate(100m, true, [ValidatedFramework()], Now);

        var option = Assert.Single(result.Options);
        Assert.Equal(18_000m, option.PlantsPerHectare);
        Assert.Equal(180m, option.Capacity);
        Assert.Empty(result.MissingInputs);
        Assert.Equal("Unit test fixture", option.SourceName);
    }

    [Fact]
    public void Calculate_rejects_region_scoped_rules_when_crop_region_does_not_match()
    {
        var result = _calculator.Calculate(100m, true, [ValidatedFramework("Region-A")], Now, "Region-B");

        Assert.Empty(result.Options);
        Assert.Contains("crop.validatedFrameRules", result.MissingInputs);
    }

    [Fact]
    public void Calculate_rejects_rules_outside_their_validity_window()
    {
        var result = _calculator.Calculate(100m, true, [ValidatedFramework(validTo: Now.AddTicks(-1))], Now);

        Assert.Empty(result.Options);
        Assert.Contains("crop.validatedFrameRules", result.MissingInputs);
    }

    private static CropFramework ValidatedFramework(string? region = null, DateTimeOffset? validTo = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Synthetic test fixture", 0.5m, 1m, 0.1m,
        "Unit test fixture", null, AgronomicParameterValidationStatus.Validated, region, null, validTo);
}
