using AgroPlan.Application.UseCases;
using AgroPlan.Domain.Logbook;
using Xunit;

namespace AgroPlan.UnitTests.Logbook;

public sealed class LogbookCorrectionTests
{
    [Fact]
    public void Create_appends_a_linked_correction_without_mutating_original_entry()
    {
        var farmId = Guid.NewGuid();
        var parcelId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
        var createdAt = occurredAt.AddMinutes(10);
        var original = new LogbookEntry(Guid.NewGuid(), farmId, parcelId, cycleId,
            LogbookActivityType.Irrigation, occurredAt, "100 liters", "producer-1", createdAt);
        var correctionTime = createdAt.AddHours(2);
        var correction = new CorrectLogbookEntry().Create(original, "Corrected measurement", occurredAt,
            "120 liters", "producer-1", correctionTime);

        Assert.NotEqual(original.Id, correction.Id);
        Assert.Equal(original.Id, correction.CorrectsEntryId);
        Assert.Equal("Corrected measurement", correction.CorrectionReason);
        Assert.Equal("120 liters", correction.Details);
        Assert.Equal(farmId, correction.FarmId);
        Assert.Equal(parcelId, correction.ParcelId);
        Assert.Equal(cycleId, correction.CropCycleId);
        Assert.Equal(correctionTime, correction.CreatedAt);
        Assert.Equal("100 liters", original.Details);
        Assert.Null(original.CorrectsEntryId);
        Assert.Null(original.CorrectionReason);
    }

    [Fact]
    public void LogbookEntry_rejects_correction_without_a_reason()
    {
        Assert.Throws<ArgumentException>(() => new LogbookEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            LogbookActivityType.Harvest, DateTimeOffset.UtcNow, "Corrected entry", "producer-1", DateTimeOffset.UtcNow,
            Guid.NewGuid(), " "));
    }
}
