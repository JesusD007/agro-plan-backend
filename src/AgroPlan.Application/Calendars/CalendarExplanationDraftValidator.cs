using System.Text.RegularExpressions;
using AgroPlan.Domain.Calendars;

namespace AgroPlan.Application.Calendars;

public static class CalendarExplanationDraftValidator
{
    public static CalendarExplanationDraft Validate(CalendarExplanationInput input, CalendarExplanationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(draft.Summary) || draft.Summary.Length > 600 ||
            Regex.IsMatch(draft.Summary, @"\d", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(draft.Summary, @"\b(seguro para cosechar|cosecha segura|phi|dosis|fertilizante|plaguicida|precio|lluvia|rendimiento|fecha)\b|ignore previous instructions|ignora las instrucciones|system prompt|revela.*(secreto|dato)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new ArgumentException("Explanation must be concise and contain no dates, numeric, safety or agronomic claims.", nameof(draft));
        if (draft.SourceIds.Count > 20 || draft.SourceIds.Any(id => !input.AllowedEvidenceIds.Contains(id, StringComparer.Ordinal)))
            throw new ArgumentException("Explanation cites unknown evidence.", nameof(draft));
        if (string.IsNullOrWhiteSpace(draft.Provider) || string.IsNullOrWhiteSpace(draft.Model) || string.IsNullOrWhiteSpace(draft.PromptVersion) || string.IsNullOrWhiteSpace(draft.SchemaVersion))
            throw new ArgumentException("Provider provenance is required.", nameof(draft));
        return draft;
    }
}
