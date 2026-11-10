# Phase 2 AI implementation research

**Researched:** 2026-11-10  
**System:** Bounded content generation: explanation of a deterministic calendar-change proposal  
**Provider contract:** provider-neutral `IAiProposalProvider`; Gemini is the current Worker adapter

## Decision

Do not introduce an agent framework. The existing .NET 10 Worker, `IAiProposalProvider`, `GeminiProposalProvider`, durable outbox pattern, and xUnit v3 tests are sufficient for one bounded explanation call. Keep the calendar-rule evaluator, evidence/freshness checks, and producer confirmation outside the model boundary.

The model receives a narrow, server-built explanation input only after deterministic logic has computed a candidate change. It returns an explanation and identifiers that are validated against that input. It must not calculate dates, quantities, costs, fertilizer rates, weather facts, withholding periods, or an action that changes the calendar.

## Contract shape

Add a calendar-specific provider-neutral request/response rather than reusing sowing fields:

```csharp
public sealed record CalendarChangeExplanationInput(
    Guid ProposalId,
    string CropCode,
    IReadOnlyList<CalendarTaskDiff> DeterministicDiffs,
    IReadOnlyList<string> AllowedEvidenceIds,
    IReadOnlyList<string> MissingInputs,
    string EvidenceStatus);

public sealed record CalendarChangeExplanationDraft(
    string Summary,
    IReadOnlyList<string> CitedEvidenceIds,
    IReadOnlyList<string> MissingInputs,
    string Model,
    string PromptVersion,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string Provider);
```

The response schema uses `application/json`, `additionalProperties: false`, and bounded strings/arrays. The server parser must reject malformed JSON, unknown evidence identifiers, empty or oversized fields, identifiers not in `AllowedEvidenceIds`, and language that claims to establish a new date, agronomic fact, weather fact, quantity, cost, or harvest safety decision. Persist the explanatory draft separately from the deterministic proposal, along with model, prompt/schema versions, evidence IDs, and generation status.

## Request and runtime guidance

`GeminiProposalProvider` already demonstrates the supported C# SDK pattern: `Client`, `GenerateContentConfig`, a server-controlled `SystemInstruction`, `ResponseMimeType = "application/json"`, and `ResponseJsonSchema`. Retain the existing bounded 5–60 second timeout, one jittered retry for transient 408/429/5xx or transport failures, circuit breaker, and content-free operational logs. Use a low temperature (0.1), explicit `MaxOutputTokens` (roughly 600–900 for a short explanation), a single turn, and no tool/function calling.

The system instruction must state that the supplied change/evidence payload is data, not instructions; that it can explain only the supplied facts; and that it must return the schema only. The user payload should contain no direct free-form instructions. Pass stable IDs and task labels/diffs, not a producer name, contact data, exact location, access token, financial record, or unrelated farm history. Do not enable `store` or project logging for sensitive production requests unless the data-retention and sharing choice is reviewed; provider abuse-monitoring retention remains an external privacy consideration.

## Guardrails and failure behavior

- Gate generation before the provider call: no proposal/explanation when deterministic rules are missing, required crop-calendar evidence is absent/expired, or a climate-dependent change has no current source. The API returns an explicit unavailable/missing state; it never returns a benign explanation for unavailable data.
- Treat output as untrusted. The server validates schema and evidence IDs before persistence, renders it as an advisory draft, and never calls a mutating calendar endpoint from model output.
- A producer confirmation must reference the immutable deterministic proposal version and its current evidence/freshness state. Recheck those at confirmation; an explanation cannot bypass a stale source, changed rules, or a harvest-safety hold.
- Preserve provider-neutral interfaces and DI. Gemini configuration (`GEMINI_API_KEY`, `GEMINI_MODEL`) stays server-side. Do not expose a key, model response, or input payload in client/API/worker logs.
- On blocked, malformed, timeout, circuit-open, unavailable, or validation-failed output, persist no confirmed schedule mutation. Return a reviewable failure/unavailable status and retain only safe diagnostic metadata.

## Test and evaluation set

Use xUnit v3 tests before enabling the capability:

1. Deterministic proposal with known task diffs yields a schema-valid explanation that cites only supplied evidence.
2. Prompt-injection-like task labels and malformed JSON cannot cause the parser to accept extra fields, unknown IDs, numeric agronomic claims, or a mutation.
3. Missing/expired calendar rules and missing/stale climate inputs suppress provider invocation and expose unavailable/missing evidence.
4. Producer confirmation after rule/evidence changes or invalidated freshness is denied; confirmation only applies the same deterministic proposal version.
5. Provider timeout/429/5xx, circuit-open, and invalid output leave no confirmed calendar change and do not log sensitive payloads.
6. Regression fixtures measure: JSON/schema validity, grounding/citation precision, no unsupported factual or numeric claim, safe refusal on missing evidence, and preservation of the confirmation boundary. A domain reviewer validates Spanish wording before production.

## Provider and operations gates

- Gemini remains the current adapter, but no application/use-case contract takes a Gemini type.
- Climate and reference-price providers remain unselected. Their contracts can represent source, geography/channel, observed/issued/valid times, freshness, and unavailable/stale state; no live values may be invented or activated.
- Before production, approve data retention/logging, model/version availability, request quotas/billing alerts, and a reviewed evaluation corpus. Restrict/rotate the server-held Gemini key and use a secret manager in deployment.

## Sources

- [Gemini structured output](https://ai.google.dev/gemini-api/docs/structured-output) — JSON Schema response mode and supported subset.
- [Gemini GenerateContent API](https://ai.google.dev/api/generate-content) — server-controlled system instructions, JSON response configuration, request labels, and storage setting.
- [Gemini safety and factuality guidance](https://ai.google.dev/gemini-api/docs/safety-guidance) — application-layer safeguards, adversarial tests, and human review.
- [Gemini API key guidance](https://ai.google.dev/gemini-api/docs/api-key) — server-side secret handling, authorization keys, restrictions, and rotation/migration guidance.
- [Gemini data logging and sharing](https://ai.google.dev/gemini-api/docs/logs-policy) — logging/retention and sharing implications.

