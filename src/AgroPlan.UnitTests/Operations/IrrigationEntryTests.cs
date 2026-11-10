using AgroPlan.Domain.Operations;
using Xunit;

namespace AgroPlan.UnitTests.Operations;

public sealed class IrrigationEntryTests
{
    [Theory]
    [InlineData(IrrigationMethod.Drip)]
    [InlineData(IrrigationMethod.Flood)]
    [InlineData(IrrigationMethod.Rain)]
    public void Entry_preserves_method_source_and_correction_history(IrrigationMethod method)
    {
        var farm = Guid.NewGuid(); var parcel = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;
        var original = new IrrigationEntry(Guid.NewGuid(), farm, parcel, null, method, "well", at, "producer", at);
        var correction = new IrrigationEntry(Guid.NewGuid(), farm, parcel, null, method, "community canal", at,
            "producer-2", at.AddMinutes(1), original.Id, "Corrected source");
        Assert.Equal("well", original.WaterSource);
        Assert.Equal(original.Id, correction.CorrectsEntryId);
        Assert.Equal("Corrected source", correction.CorrectionReason);
        Assert.Equal("community canal", correction.WaterSource);
    }

    [Fact]
    public void Entry_rejects_blank_water_source_or_missing_correction_reason()
    {
        Assert.Throws<ArgumentException>(() => new IrrigationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            IrrigationMethod.Drip, " ", DateTimeOffset.UtcNow, "producer", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new IrrigationEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            IrrigationMethod.Drip, "well", DateTimeOffset.UtcNow, "producer", DateTimeOffset.UtcNow, Guid.NewGuid(), " "));
    }
}
