using AgroPlan.Application.Abstractions;
using AgroPlan.Worker.AI;
using Xunit;

namespace AgroPlan.UnitTests.Planning;

public sealed class GeminiProposalResponseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseResponse_rejects_missing_raw_json(string? json)
    {
        Assert.Throws<GeminiProviderException>(() => GeminiProposalProvider.ParseResponse(json, CreateInput(), "gemini-test"));
    }

    [Fact]
    public void ParseResponse_rejects_malformed_json()
    {
        Assert.Throws<GeminiProviderException>(() => GeminiProposalProvider.ParseResponse("{ malformed", CreateInput(), "gemini-test"));
    }

    [Theory]
    [InlineData("{\"missingInputs\":[],\"sourceIds\":[]}")]
    [InlineData("{\"summary\":null,\"missingInputs\":[],\"sourceIds\":[]}")]
    [InlineData("{\"summary\":\"   \",\"missingInputs\":[],\"sourceIds\":[]}")]
    [InlineData("{\"summary\":\"Review the evidence.\",\"sourceIds\":[]}")]
    [InlineData("{\"summary\":\"Review the evidence.\",\"missingInputs\":[]}")]
    public void ParseResponse_rejects_missing_null_or_empty_summary(string json)
    {
        Assert.Throws<GeminiProviderException>(() => GeminiProposalProvider.ParseResponse(json, CreateInput(), "gemini-test"));
    }

    [Theory]
    [InlineData("A planting date of 4 is recommended.")]
    [InlineData("Use 20 kilograms of input.")]
    public void ParseResponse_rejects_numeric_advice(string summary)
    {
        var json = $"{{\"summary\":\"{summary}\",\"missingInputs\":[],\"sourceIds\":[]}}";

        Assert.Throws<GeminiProviderException>(() => GeminiProposalProvider.ParseResponse(json, CreateInput(), "gemini-test"));
    }

    [Fact]
    public void ParseResponse_rejects_sources_not_present_in_the_input()
    {
        const string json = "{\"summary\":\"Review the supplied evidence.\",\"missingInputs\":[],\"sourceIds\":[\"invented\"]}";

        Assert.Throws<GeminiProviderException>(() => GeminiProposalProvider.ParseResponse(json, CreateInput(), "gemini-test"));
    }

    [Fact]
    public void ParseResponse_rejects_malformed_missing_input_identifiers()
    {
        const string json = "{\"summary\":\"Review the supplied evidence.\",\"missingInputs\":[\"unknown input with spaces\"],\"sourceIds\":[]}";

        Assert.Throws<ArgumentException>(() => GeminiProposalProvider.ParseResponse(json, CreateInput(), "gemini-test"));
    }

    [Fact]
    public void ParseResponse_returns_valid_provider_and_model_provenance()
    {
        const string json = "{\"summary\":\"Review the supplied evidence with a qualified specialist.\",\"missingInputs\":[],\"sourceIds\":[\"weather-source\"]}";

        var draft = GeminiProposalProvider.ParseResponse(json, CreateInput(), "gemini-test-model");

        Assert.Equal("gemini", draft.Provider);
        Assert.Equal("gemini-test-model", draft.Model);
        Assert.Equal(GeminiProposalProvider.PromptVersion, draft.PromptVersion);
        Assert.Equal(GeminiProposalProvider.SchemaVersion, draft.SchemaVersion);
        Assert.NotEqual(default, draft.GeneratedAt);
        Assert.Equal(new[] { "weather-source" }, draft.SourceIds);
    }

    private static SowingGenerationInput CreateInput() => new(
        CropCode: "PLANTAIN",
        CropName: "Plátano",
        CropValidationStatus: "Validated",
        CropRegion: "Test region",
        CropValidFrom: null,
        CropValidTo: null,
        DesiredPlantingDate: new DateOnly(2026, 12, 1),
        ParcelAreaHectares: 1.25m,
        LocationDescription: "Test location",
        ValidatedParameters: [],
        Climate: new ClimateContext(
            "available",
            "weather-provider",
            "Weather office",
            Now,
            Now,
            Now,
            Now.AddDays(7),
            "Test region",
            "fresh",
            null,
            [],
            ["weather-source"]));
}
