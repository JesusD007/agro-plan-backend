# AI-SPEC — Phase 2: Farm Operations and Safety

> AI design contract generated through `$gsd-ai-integration-phase`. Consumed by the Phase 2 planner, executors, verifier, and eval auditor.
> The contract covers only explanations of deterministic calendar-change proposals. Operational records, safety rules, reports, and external facts remain deterministic application concerns.

---

## 1. System Classification

**System Type:** Content — bounded, structured explanation of an already-computed calendar change.

**Phase Goal:** A producer can maintain operational records and use them to make safer, better-informed farm decisions.

**AI Scope:** After AgroPlan's deterministic calendar rules produce an immutable, versioned proposal containing task/date differences and source identifiers, the Worker may ask the configured AI provider to explain those differences in Spanish. The explanation is an advisory draft. It cannot create or change a proposal, calendar task, harvest window, safety hold, cost, quantity, agronomic rule, climate fact, or price fact. The producer explicitly confirms the deterministic proposal; confirmation does not approve model-authored data.

**Out of AI Scope:**

- PHI and harvest eligibility, including exact product/crop/use matching and earliest safe date.
- Crop stage dates, fertilizer rates, application frequency, quantities, units, costs, conversions, reference prices, and weather facts.
- Selection or implementation of climate and price providers. Both remain unconfigured production gates.
- Farm/advisor authorization, report generation, record correction, or any other mutating tool call.

### Critical Failure Modes

1. **Unsupported fact or value:** the explanation invents or modifies a date, quantity, rate, cost, PHI, weather fact, price, agronomic rule, or evidence identifier.
2. **Authority boundary bypass:** model output directly mutates a calendar or causes confirmation without the producer confirming the exact deterministic proposal version.
3. **Stale or missing evidence treated as safe:** explanation runs when a required calendar rule is absent/expired or when a climate-dependent proposal lacks a current configured source.
4. **Prompt injection through farm data:** a task label, note, or source field is treated as an instruction and changes the provider's role or output contract.
5. **Sensitive or cross-farm disclosure:** a request, response, trace, or log includes another farm's data, credentials, personal contact data, financial records, exact location, or unrelated operation history.
6. **Invalid output accepted:** malformed JSON, extra fields, unknown evidence IDs, oversized text, or ungrounded claims are persisted as a usable explanation.

---

## 1b. Domain Context and Expert Criteria

**Industry Vertical:** Farm operations and crop-cycle management in the Dominican Republic.

**User Population:** Producers and farm administrators who manage crop calendars; authorized advisors may read an explanation only when its category is in a current grant. Agronomists and extension specialists validate calendar evidence and evaluation fixtures.

**Stakes Level:** High. A misleading explanation may cause a user to accept an inappropriate schedule change, apply an input at the wrong time, or misunderstand a harvest restriction.

### Domain Rubric Ingredients

| Dimension | Good — expert accepts | Bad — expert rejects | Stakes |
|---|---|---|---|
| Deterministic proposal fidelity | Explanation describes only task/date differences already present in the immutable proposal and cites only its allowed evidence IDs. | Adds a new task/date, changes a difference, or cites evidence outside the proposal. | Critical |
| Safety boundary | Explanation never declares harvest safe and cannot weaken an unresolved PHI hold; safety status comes only from deterministic exact-label logic. | Uses AI, MRL, generic ingredient data, or an unrelated crop label to clear or reinterpret a hold. | Critical |
| Evidence and freshness | Every described reason maps to current calendar evidence or a current configured climate advisory with source, geography, issue/validity window, and version. | Treats missing, stale, or wrong-area data as current or as proof of no risk. | Critical |
| Missing agronomic configuration | Missing calendar or crop-stage rules produce an explicit unavailable/missing state before provider invocation. | The model fills a gap with a plausible stage, fertilizer rate, threshold, or calendar date. | Critical |
| Reviewability and language | Spanish wording is clear, concise, advisory, identifies why the deterministic proposal exists, and states that the producer must confirm it. | Presents the explanation as an instruction, approved technical advice, or confirmed change. | High |
| Privacy and farm scope | Provider input contains only bounded positional task labels, deterministic date differences, and semantic evidence labels for one authorized proposal. Internal GUIDs, task keys, source-version strings, farm/cycle identifiers, and raw evidence IDs remain server-side. | Includes names, contacts, credentials, exact location, raw financial entries, internal identifiers, full logbook text, or another farm's records. | Critical |

### Expert Roles

| Role | Evaluation responsibility |
|---|---|
| Dominican agronomist or extension specialist familiar with the pilot crops | Label whether the Spanish explanation stays within supplied calendar evidence, abstains correctly, and avoids unsupported agronomic meaning. |
| Food-safety or pesticide-label specialist | Confirm test cases never allow explanations to establish or override PHI/harvest eligibility. |
| Producer or farm administrator | Review clarity, usefulness, advisory wording, and confirmation expectations. |
| AgroPlan product/security reviewer | Validate farm scope, data minimization, provider-neutral behavior, logs, and confirmation/version controls. |

---

## 2. Framework Decision

**Selected Approach:** No additional AI/agent framework. Keep the direct `Google.GenAI` C# SDK in the Worker behind AgroPlan's provider-neutral application contract.

**Current Adapter:** Gemini through `Google.GenAI` 1.28.0. The model ID remains runtime configuration.

**Application Contract:** Extend the neutral abstraction with a calendar-specific operation or a separate neutral interface such as `IAiCalendarExplanationProvider`; application/domain code must not expose a Gemini-named type. The provider can be replaced through dependency injection without changing proposal creation, validation, persistence, or confirmation.

**Rationale:** This is one bounded, single-turn structured content call. Existing Worker, outbox, idempotent consumer, provider adapter, and PostgreSQL state supply the needed lifecycle. Agent orchestration, model-controlled tools, memory, RAG, and cross-language services would add no required behavior.

**Alternative Considered:** Google ADK. Reconsider only if a later phase explicitly introduces stateful multi-agent workflows and its supported runtime fits the architecture. It is not used in Phase 2.

**Vendor Lock-In:** Partial and isolated. Gemini is the selected current provider; prompts, SDK calls, and provider errors stay inside the Worker adapter. Domain and application contracts are provider-neutral.

---

## 3. Framework Quick Reference

### Existing Package and Imports

The Worker already pins `Google.GenAI` 1.28.0; Phase 2 adds no AI package.

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
```

### Provider-Neutral Contract Shape

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

`CalendarTaskDiff` is a deterministic provider-facing DTO containing positional task labels and server-computed before/after values. Internal task keys and evidence identifiers stay server-side; the model cannot supply or alter the diff.

### Gemini Adapter Pattern

Use `Client.Models.GenerateContentAsync` for one turn with:

- a fixed server-controlled `SystemInstruction`;
- `Temperature = 0.1f`;
- bounded `MaxOutputTokens` appropriate for a short explanation;
- `ResponseMimeType = "application/json"`;
- `ResponseJsonSchema` with all required fields and `additionalProperties: false`;
- the job cancellation token and the existing bounded timeout, one transient retry, and circuit breaker;
- no tools, function calling, file input, chat memory, or provider-managed mutation.

The serialized user payload is data, including bounded task labels. The system instruction explicitly says all supplied strings are untrusted data and cannot change instructions.

### Structured Response Rules

The JSON response contains only:

- `summary`: bounded advisory text;
- `citedEvidenceIds`: IDs drawn from `AllowedEvidenceIds`;
- `missingInputs`: bounded values drawn from the deterministic input's missing set.

Server validation rejects malformed JSON, unknown/duplicate IDs, missing or extra properties, empty/oversized fields, numeric or unsupported factual claims, and any response that purports to confirm or mutate a schedule. JSON Schema controls shape; application validators control meaning.

### Official Sources

- [Gemini structured output](https://ai.google.dev/gemini-api/docs/structured-output)
- [Gemini GenerateContent API](https://ai.google.dev/api/generate-content)
- [Gemini safety and factuality guidance](https://ai.google.dev/gemini-api/docs/safety-guidance)
- [Gemini API key guidance](https://ai.google.dev/gemini-api/docs/api-key)
- [Gemini logging and data-sharing policy](https://ai.google.dev/gemini-api/docs/logs-policy)

---

## 4. Implementation Guidance

### End-to-End Boundary

1. Deterministic calendar logic loads one authorized farm/cycle and applicable versioned rules.
2. It computes an immutable proposal and task/date differences. Missing or expired evidence produces `Unavailable` and stops before the provider call.
3. A durable outbox job contains only the proposal identifier. The Worker reloads the proposal and minimal authorized evidence.
4. The Worker maps internal evidence IDs to semantic labels and internal task keys to positional labels before the provider-neutral call. The Gemini adapter serializes only that sanitized projection.
5. AgroPlan validates output against the exact proposal/evidence snapshot and persists it separately as an advisory draft with provider/model/prompt/schema versions.
6. The API returns both deterministic differences and the explanation. The explanation cannot be submitted as mutation input.
7. Producer confirmation references the deterministic proposal ID/version. The server rechecks current farm membership, proposal state, evidence/rule versions, freshness, and every safety hold before applying the proposed calendar changes.

### Data and State

- PostgreSQL is authoritative for proposal input versions, deterministic differences, explanation state, cited evidence, provider metadata, and confirmation.
- Persist no raw provider request/response beyond the validated draft needed by the product. Logs contain stable job/proposal IDs, duration, attempt, provider/model version, token counts when safely available, and a bounded failure category.
- Before calling any provider, project immutable proposal data to generic positional task labels and semantic evidence labels (`validated_crop_calendar`, `confirmed_planting_record`). Never serialize proposal/farm/cycle/template/sowing GUIDs, opaque task keys, raw source-version strings, or internal evidence identifiers to the provider. Validate returned citations against only those semantic labels.
- Do not send producer identity, advisor identity, contact information, credentials, exact coordinates, raw financial rows, unrelated logbook entries, or full application records.
- Do not cache across farms. Any exact-result reuse would require a farm-safe hash of every deterministic input and evidence version and is outside Phase 2.

### Safe Failure Behavior

- Not configured, stale evidence, timeout, rate limit, provider failure, circuit open, blocked content, malformed JSON, and validation failure all leave the deterministic proposal unconfirmed.
- No repair prompt is required. If implementation permits one schema repair attempt, it receives the same authorized inputs plus a bounded machine-generated validation category and may not introduce data.
- Climate and reference-price integrations stay unavailable/stale until their separate provider, geography/channel mapping, freshness, and operational policies are approved. This AI contract does not select them.
- An explanation can summarize pending deterministic calendar tasks for harvest preparation, but it never estimates the harvest window or clears the PHI gate.

### Configuration and Secrets

- `GEMINI_API_KEY` and `GEMINI_MODEL` remain Worker-side secrets/configuration.
- Retain the existing 5–60 second timeout bound, one jittered retry only for transport/408/429/5xx failures, and circuit breaker.
- Review provider data retention/log sharing, model availability, quotas, billing alerts, and key restriction/rotation before production.

---

## 5. Evaluation Strategy

### Blocking Evaluation Dimensions

| Dimension | Priority | PASS | FAIL | Measurement |
|---|---|---|---|---|
| Deterministic fidelity and task completion | Critical | Output is valid JSON, describes every requested proposal difference without changing it, and cites only supplied evidence IDs. | Adds/omits/changes a difference, invents a task/date, cites an unknown source, or cannot produce the required advisory explanation. | Code: xUnit v3 exact set comparisons and schema/validator tests. Human: sampled wording review. |
| Safety boundary | Critical | Output contains no PHI decision, harvest-safety claim, agronomic rate/threshold, cost, price, quantity, weather assertion, or model-authored date. | Any prohibited claim appears or the explanation weakens an unresolved hold. | Code: typed contract, forbidden-claim validators, persistence/state assertions. Human specialist review for semantic euphemisms. |
| Evidence availability and freshness | Critical | Missing/expired rules and missing/stale climate evidence prevent provider invocation and return explicit missing/unavailable status. | Provider is called or a reassuring explanation is returned without current required evidence. | Code: fake provider invocation count, fixed clock, freshness/version tests. |
| Confirmation integrity | Critical | Only an authorized producer confirms the exact immutable proposal version after rule/evidence/freshness and safety rechecks. | AI output, a retry, stale proposal, advisor, revoked user, or changed evidence confirms or mutates a calendar. | Code: xUnit unit and PostgreSQL/API integration state-transition tests. |
| Prompt-injection and output validation | Critical | Adversarial labels remain data; extra fields, unknown IDs, malformed JSON, oversized fields, and unsupported claims are rejected without mutation. | Farm text changes instructions, output escapes the schema, or invalid content is persisted as usable. | Code: adversarial fixture matrix. Human security review of novel cases. |
| Privacy and isolation | Critical | Request/log/trace contains only one farm's minimal stable proposal data and safe metadata. | Secrets, personal/contact data, raw finances, exact location, unrelated records, or cross-farm data appear. | Code: serialization allowlist, log-capture and cross-farm integration tests. Security review. |
| Spanish clarity and non-authoritative tone | High | Producer and agronomist understand what moved and why; wording is concise, advisory, names missing evidence, and asks for confirmation. | Wording sounds mandatory/approved, obscures uncertainty, or misstates the deterministic result. | Human: agronomist and producer labels. LLM judge only after calibration to human labels with correlation at least 0.7. |
| Provider replaceability | High | Application/consumer tests use a fake neutral provider and pass without Gemini types, credentials, or network calls. | Application logic imports Gemini types or tests require a live provider/key. | Code: compile-time dependencies, DI replacement test, no-network test suite. |

### Required xUnit v3 Test Set

Tests are the blocking Phase 2 CI gate and use fixed clocks, fake providers, synthetic farm data, and isolated PostgreSQL containers. They require no production credential or live AI call.

1. Known deterministic differences yield a schema-valid draft whose cited IDs are an exact subset of allowed evidence.
2. Malformed JSON, extra properties, unknown/duplicate IDs, empty/oversized fields, and unsupported numeric/factual claims are rejected.
3. Prompt-injection strings in task labels, crop text, and evidence labels cannot add output fields, instructions, dates, sources, or mutation state.
4. Missing/expired rules and missing/stale climate inputs suppress provider invocation and expose unavailable/missing evidence.
5. Changed evidence/rule versions, invalidated freshness, safety holds, revoked membership, wrong farm, or advisor identity deny confirmation.
6. Timeout, 429/5xx, circuit-open, blocked, and invalid output preserve an unconfirmed proposal and emit only safe diagnostic metadata.
7. A fake replacement provider can generate a valid explanation through the same application contract without changing consumer behavior.
8. Logs/traces do not include request payloads, secrets, personal/contact data, raw financial records, or another farm's identifiers.

**Blocking commands:**

```powershell
.\.tools\dotnet\dotnet.exe test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~CalendarExplanation" --no-restore -m:1
.\.tools\dotnet\dotnet.exe test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~CalendarProposal" --no-restore -m:1
```

### Eval Tooling

**Current blocking tooling:** existing xUnit v3 3.2.2, `Microsoft.NET.Test.Sdk` 18.11.0, Testcontainers PostgreSQL/RabbitMQ, fake provider adapters, and log capture. This matches the .NET-only implementation and runs offline.

**Prompt regression candidate:** Promptfoo may be added only after a non-production model endpoint/key is approved. It is not a Phase 2 build dependency or substitute for xUnit domain/security assertions.

```bash
npm install --save-dev promptfoo
npx promptfoo eval --config evals/calendar-explanations.yaml
```

**Trace inspection candidate:** self-hosted Arize Phoenix with OpenTelemetry is the GSD default when no tracing platform exists. It is a separate observability service, not an application framework, and is production-gated by privacy/retention review.

```bash
pip install arize-phoenix opentelemetry-sdk
phoenix serve
```

Until that gate is approved, use the existing structured .NET logs and persisted safe provider metadata. Never send raw farm prompts or outputs to tracing.

### Reference Dataset

**Initial size:** 20 labeled cases before production activation; at least 10 exist during implementation before enabling the provider path.

**Composition:**

- four normal deterministic proposal cases across multiple pilot crops;
- four missing/expired evidence and climate-unavailable/stale cases;
- three safety cases with unresolved PHI or harvest-related pending work;
- four adversarial cases covering prompt injection, unknown sources, extra JSON, and numeric/agronomic claims;
- three confirmation cases covering version drift, revocation/cross-farm access, and advisor read-only identity;
- two provider failure/privacy cases covering timeout/retry/circuit behavior and log redaction.

**Labels:** Domain expert supplies expected evidence coverage, acceptable abstention, prohibited semantic claims, and Spanish wording rating. Product/security supplies expected state transition, authorization, farm isolation, and data-minimization labels. Engineering supplies exact schema/status/error expectations. Disagreements are resolved before the case becomes a gold fixture.

**Creation timeline:** Build fixtures alongside implementation and run them from the first provider-validator commit. Add production failures only after redaction and expert relabeling; never copy private farm payloads into the dataset.

---

## 6. Guardrails

### Online Guardrails — Every Request

| Guardrail | Trigger | Intervention |
|---|---|---|
| Deterministic evidence pre-gate | Required calendar rule is missing/expired, proposal has no immutable version/diffs, or climate-dependent reasoning lacks current configured evidence. | Do not invoke provider; return explicit `Unavailable`/missing inputs. |
| Minimal farm-scoped serialization | Input contains a disallowed field/category or resource from a different farm/cycle. | Reject job, persist safe failure category, raise security telemetry. |
| Strict structured-output validation | JSON/schema invalid; extra/oversized/empty field; evidence ID outside allowlist; unsupported factual/numeric/safety claim. | Reject draft; never expose it as usable or mutate the calendar. |
| Immutable proposal and confirmation recheck | Proposal/evidence/rule/freshness version changed, membership revoked, advisor attempts write, or safety hold unresolved. | Deny confirmation and require a fresh deterministic proposal. |
| Secrets and sensitive logging | Credential, raw payload, personal/contact, exact location, financial or cross-farm data reaches log/trace fields. | Suppress/redact event and create bounded security incident metadata. |

### Offline Quality Flywheel

| Metric | Sampling | Action on degradation |
|---|---|---|
| Evidence citation precision/recall | All rejected drafts plus stratified samples by crop/model/prompt/schema version. | Disable affected version; inspect validator/prompt and relabel reference cases. |
| Unsupported-claim and semantic safety rate | All validator hits and every safety-related explanation; human specialist review. | Block provider path until cause is fixed and regression case passes. |
| Abstention correctness | Sample unavailable/missing cases and user retries by missing field. | Improve evidence acquisition or error clarity; do not relax abstention. |
| Human wording acceptance | Expert review of every corpus release and weighted production sample. | Revise prompt/schema and re-run gold set; calibrate any judge again. |
| Confirmation/rejection/edit rate | Sample producer confirmations, rejections, and requested refreshes without storing private content. | Investigate evidence/prompt version and add redacted labeled case. |

---

## 7. Production Monitoring

### Metrics

- provider invocation, success, timeout, transient retry, circuit-open, blocked, parse-failure, and validation-failure counts by provider/model/prompt/schema version;
- latency p50/p95, safe token-usage totals when available, and cost estimate after the model/rate is approved;
- unavailable/abstention rate by crop and missing-input category;
- evidence citation precision, unsupported-claim rejection rate, and schema-valid rate;
- proposal confirmation denied for drift/freshness/safety/auth reasons, and confirmation/rejection rate;
- cross-farm authorization denials and sensitive-field/log guardrail hits.

### Alert Thresholds

| Signal | Threshold | Response |
|---|---|---|
| Cross-farm or secret/personal-data disclosure | Any occurrence | Disable affected path immediately; security incident and forensic review. |
| AI-created mutation or confirmation bypass | Any occurrence | Disable explanation/confirmation path; integrity incident and data audit. |
| Unsupported safety/agronomic/numeric claim accepted | Any occurrence | Quarantine draft/version, disable model/prompt version, specialist review. |
| Evidence/freshness pre-gate bypass | Any occurrence | Disable affected rule path and re-run full reference dataset. |
| Schema/validation/provider/latency degradation | Establish numeric thresholds from a non-production baseline; no invented SLO. | Alert on sustained deviation, keep safe unavailable behavior, and review before re-enable. |

### Sampling Strategy

- Review 100% of guardrail failures, repairs, provider retries/timeouts, stale/missing evidence cases, version-drift denials, and safety-related explanations.
- Oversample new crops, rule versions, prompt/schema/model versions, and explanations rejected or refreshed by producers.
- Add a small stratified random sample of successful routine explanations so unknown failure modes can surface.
- Trace only redacted features, stable IDs/hashes, versions, statuses, timings, token counts, and label outcomes. Never trace raw prompts, credentials, personal/contact data, exact coordinates, raw finances, or unrelated operation history.

### Production Activation Gates

The AI explanation path remains disabled until all are true:

1. All xUnit v3 unit/integration gates pass and the 20-case expert-labeled reference set has zero critical failures.
2. An agronomist/extension specialist approves Spanish wording and abstention behavior.
3. Provider model/version, quota, billing, key restriction/rotation, and data retention/log sharing are approved.
4. Monitoring/redaction and disable controls are verified.
5. Any climate-backed explanation waits for a separately approved climate provider, geography mapping, freshness policy, and evidence contract. Reference-price sources are likewise selected separately and are never inferred by this model.

---

## Acceptance Checklist

- [x] System classified as bounded content generation.
- [x] Six critical failure modes documented.
- [x] Gemini kept behind a provider-neutral contract with no new agent framework.
- [x] Deterministic proposal and producer-confirmation boundaries specified.
- [x] Domain-specific rubrics include code, human, and calibrated-judge measurement paths.
- [x] xUnit v3 offline unit/integration eval suite specified with no production keys or live calls.
- [x] Reference dataset size, composition, labels, and creation timing specified.
- [x] Online guardrails and offline flywheel defined.
- [x] Production metrics, alert policy, smart sampling, and activation gates defined.
- [x] Climate and reference-price provider decisions remain unresolved production gates.
