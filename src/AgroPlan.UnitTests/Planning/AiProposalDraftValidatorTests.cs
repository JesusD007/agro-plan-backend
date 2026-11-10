using AgroPlan.Application.Abstractions;
using AgroPlan.Application.Planning;
using Xunit;

namespace AgroPlan.UnitTests.Planning;

public sealed class AiProposalDraftValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_accepts_a_complete_draft_with_only_input_sources()
    {
        var input = CreateInput();
        var draft = CreateDraft(sourceIds: ["Agronomy handbook", "https://example.test/handbook", "weather-source"]);

        var validated = AiProposalDraftValidator.Validate(input, draft);

        Assert.Same(draft, validated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_missing_summary(string summary)
    {
        var draft = CreateDraft(summary: summary);

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Fact]
    public void Validate_rejects_summary_longer_than_the_contract_limit()
    {
        var draft = CreateDraft(summary: new string('a', 601));

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Theory]
    [InlineData("plant on day 4")]
    [InlineData("use 20 kilograms")]
    [InlineData("temperature is 28 degrees")]
    public void Validate_rejects_numeric_claims_in_summary(string summary)
    {
        var draft = CreateDraft(summary: summary);

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Fact]
    public void Validate_rejects_a_source_not_present_in_the_generation_input()
    {
        var draft = CreateDraft(sourceIds: ["invented-source"]);

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("x")]
    [InlineData("this missing-input identifier is intentionally longer than one hundred characters and must be rejected by the provider contract")]
    public void Validate_rejects_missing_input_identifiers_outside_the_contract(string missingInput)
    {
        var draft = CreateDraft(missingInputs: [missingInput]);

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Theory]
    [InlineData("provider", "", "prompt-v1", "schema-v1")]
    [InlineData("provider", "model-v1", "", "schema-v1")]
    [InlineData("provider", "model-v1", "prompt-v1", "")]
    [InlineData("", "model-v1", "prompt-v1", "schema-v1")]
    public void Validate_rejects_missing_provider_provenance(
        string provider,
        string model,
        string promptVersion,
        string schemaVersion)
    {
        var draft = CreateDraft(
            model: model,
            promptVersion: promptVersion,
            schemaVersion: schemaVersion,
            provider: provider);

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Fact]
    public void Validate_rejects_a_default_generation_timestamp()
    {
        var draft = CreateDraft(generatedAt: DateTimeOffset.MinValue);

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
    }

    [Fact]
    public void Validate_rejects_null_collections_from_an_untrusted_provider()
    {
        var draft = CreateDraft() with { SourceIds = null!, MissingInputs = null! };

        Assert.Throws<ArgumentException>(() => AiProposalDraftValidator.Validate(CreateInput(), draft));
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
        ValidatedParameters:
        [
            new ValidatedAgronomicInput(
                "soil.preparation",
                1m,
                "unit",
                "Agronomy handbook",
                "https://example.test/handbook",
                "Test region",
                null,
                null)
        ],
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

    private static AiProposalDraft CreateDraft(
        string summary = "The supplied evidence is ready for producer review.",
        IReadOnlyList<string>? sourceIds = null,
        IReadOnlyList<string>? missingInputs = null,
        string model = "gemini-test-model",
        string promptVersion = "sowing-draft-v1",
        string schemaVersion = "sowing-json-v1",
        string provider = "gemini",
        DateTimeOffset? generatedAt = null) => new(
            summary,
            sourceIds ?? ["Agronomy handbook"],
            missingInputs ?? [],
            model,
            promptVersion,
            schemaVersion,
            generatedAt ?? Now,
            provider);
}
