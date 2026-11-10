using System.Text.RegularExpressions;
using AgroPlan.Application.Abstractions;

namespace AgroPlan.Application.Planning;

/// <summary>
/// Validates untrusted output from any AI provider before it is persisted as a proposal draft.
/// Provider adapters may add stricter checks, but cannot replace this shared boundary.
/// </summary>
public static partial class AiProposalDraftValidator
{
    [GeneratedRegex(@"\d", RegexOptions.CultureInvariant)]
    private static partial Regex NumericClaimPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._:-]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingInputPattern();

    public static AiProposalDraft Validate(SowingGenerationInput input, AiProposalDraft draft)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(draft);

        if (string.IsNullOrWhiteSpace(draft.Summary) || draft.Summary.Length > 600)
            throw new ArgumentException("AI draft summary must contain between 1 and 600 characters.", nameof(draft));
        if (NumericClaimPattern().IsMatch(draft.Summary))
            throw new ArgumentException("AI draft summary contains an unsupported numeric claim.", nameof(draft));
        if (draft.SourceIds is null || draft.MissingInputs is null)
            throw new ArgumentException("AI draft collections must be present.", nameof(draft));
        if (string.IsNullOrWhiteSpace(draft.Provider) || draft.Provider.Length > 100 ||
            string.IsNullOrWhiteSpace(draft.Model) || draft.Model.Length > 200 ||
            string.IsNullOrWhiteSpace(draft.PromptVersion) || draft.PromptVersion.Length > 100 ||
            string.IsNullOrWhiteSpace(draft.SchemaVersion) || draft.SchemaVersion.Length > 100 ||
            draft.GeneratedAt == default)
            throw new ArgumentException("AI draft provenance metadata is incomplete or invalid.", nameof(draft));

        var allowedSourceIds = input.ValidatedParameters
            .Select(parameter => parameter.SourceName)
            .Concat(input.ValidatedParameters.Select(parameter => parameter.SourceUrl).OfType<string>())
            .Concat(input.Climate.SourceIds)
            .ToHashSet(StringComparer.Ordinal);
        if (draft.SourceIds.Any(sourceId => string.IsNullOrWhiteSpace(sourceId) || !allowedSourceIds.Contains(sourceId)))
            throw new ArgumentException("AI draft contains an unknown source identifier.", nameof(draft));
        if (draft.MissingInputs.Any(missing => string.IsNullOrWhiteSpace(missing) || !MissingInputPattern().IsMatch(missing)))
            throw new ArgumentException("AI draft contains an invalid missing-input identifier.", nameof(draft));

        return draft;
    }
}
