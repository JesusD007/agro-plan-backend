using AgroPlan.Application.Crops;
using AgroPlan.Domain.Crops;
using Xunit;

namespace AgroPlan.UnitTests.Crops;

public sealed class CropCatalogServiceTests
{
    private static readonly string[] RequiredParameters =
    [
        "variety", "plantingWindow", "spacing", "density", "cycleDuration",
        "growthStages", "waterRequirement", "zoneSuitability"
    ];

    [Fact]
    public async Task ListAsync_exposes_the_five_approved_crop_identities_without_invented_values()
    {
        var repository = new FakeCropCatalogRepository(
        [
            Entry("platano", "Plátano"),
            Entry("yuca", "Yuca"),
            Entry("batata", "Batata"),
            Entry("arroz", "Arroz"),
            Entry("habichuela", "Habichuela")
        ]);
        var service = new CropCatalogService(repository);

        var result = await service.ListAsync(CancellationToken.None);

        Assert.Equal(["ARROZ", "BATATA", "HABICHUELA", "PLATANO", "YUCA"],
            result.Select(item => item.Code).Order(StringComparer.Ordinal));
        Assert.All(result, item =>
        {
            Assert.Empty(item.ValidatedParameters);
            Assert.Equal(RequiredParameters.Order(StringComparer.Ordinal), item.MissingParameters.Order(StringComparer.Ordinal));
            Assert.Equal(CropCatalogValidationStatus.IdentityOnly.ToString(), item.ValidationStatus);
        });
    }

    [Fact]
    public async Task ListAsync_does_not_expose_region_scoped_values_without_a_matching_zone()
    {
        var crop = Entry("arroz", "Arroz");
        crop.Parameters.Add(new AgronomicParameter(
            Guid.NewGuid(), crop.Id, "spacing", 0.25m, "m", AgronomicParameterValidationStatus.Validated,
            "Test fixture only", null, "Region-A", null, null));
        var service = new CropCatalogService(new FakeCropCatalogRepository([crop]));

        var result = await service.ListAsync(CancellationToken.None);

        Assert.Empty(Assert.Single(result).ValidatedParameters);
        Assert.Contains("spacing", result[0].MissingParameters);
    }


    private static CropCatalogEntry Entry(string code, string displayName) => new(
        Guid.NewGuid(), code, displayName, "Pilot identity source",
        CropCatalogValidationStatus.IdentityOnly, null, null, null);

    private sealed class FakeCropCatalogRepository(IReadOnlyList<CropCatalogEntry> entries) : ICropCatalogRepository
    {
        public Task<IReadOnlyList<CropCatalogEntry>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(entries);
    }
}
