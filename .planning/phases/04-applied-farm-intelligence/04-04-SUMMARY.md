---
phase: 04-applied-farm-intelligence
plan: 04
subsystem: api, worker, testing
tags: [dotnet-10, xunit-v3, aspnet-core, scalar, openapi, provider-gates, ai-eval]
requires:
  - phase: 04-01
    provides: Provider-neutral voice drafts, consent, and confirmation workflow
  - phase: 04-02
    provides: Weekly summaries, deterministic projections, and scoped sharing
  - phase: 04-03
    provides: Symptom reports, manual review, and private media lifecycle
provides:
  - Conditional, default-off Worker registration for voice, summary, and symptom consumers
  - Explicit operation approval gates for narrative and symptom providers
  - Bilingual speech activation gate requiring provider evidence and per-language accuracy
  - Cross-feature OpenAPI/Scalar and per-farm request-limit regression tests
  - Offline metadata-only AI-SPEC fixture manifest with 45 deterministic evaluation cases
affects: [phase-04-verification, provider-selection, ai-evaluation]
actuals:
  tokens: 14437
  tasks: 3
  commits: 5
tech-stack:
  added: []
  patterns: [provider-neutral ports, fail-closed opt-in workers, Category=AiEval fictional fixtures]
key-files:
  created:
    - src/AgroPlan.Worker/AppliedFarmIntelligenceWorkerRegistration.cs
    - src/AgroPlan.Worker/AI/UnconfiguredSpeechTranscriptionProvider.cs
    - src/AgroPlan.Worker/AI/UnconfiguredWeeklySummaryProvider.cs
    - src/AgroPlan.Worker/AI/UnconfiguredSymptomObservationProvider.cs
    - src/AgroPlan.IntegrationTests/Api/AppliedFarmIntelligenceContractTests.cs
    - src/AgroPlan.UnitTests/AI/Phase04EvaluationCases.cs
    - src/AgroPlan.UnitTests/AI/Phase04EvaluationTests.cs
  modified:
    - src/AgroPlan.Worker/Program.cs
    - src/AgroPlan.Worker/Jobs/WeeklySummaryConsumer.cs
    - src/AgroPlan.Application/Abstractions/IWeeklySummaryNarrativeProvider.cs
    - src/AgroPlan.IntegrationTests/Worker/AppliedFarmIntelligenceWorkerActivationTests.cs
    - src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs
key-decisions:
  - "All three Phase 4 provider operations remain off unless an operation-specific adapter and explicit configuration gate are present."
  - "Narrative and symptom providers require a configured provider name, the exact verified operation, and approved evidence; speech also requires both languages and the full validation threshold."
  - "The 45 synthetic AI-SPEC descriptors validate deterministic boundaries only and never count as real-provider accuracy evidence."
patterns-established:
  - "Optional AI consumers are registered in one Worker boundary and remain separate from Gemini proposal handling."
  - "Unconfigured development/test adapters report typed unavailable states without network access."
requirements-completed: [REQ-rf-g-39, REQ-rf-g-40, REQ-rf-g-43]
coverage:
  - id: D1
    description: Phase 4 Worker consumers require feature-specific opt-in and approved provider configuration; speech stays bilingual and evidence-gated.
    requirement: REQ-rf-g-39
    verification:
      - kind: integration
        ref: "src/AgroPlan.IntegrationTests/Worker/AppliedFarmIntelligenceWorkerActivationTests.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: Development OpenAPI documents all Phase 4 routes and Scalar loads; Production hides both documentation routes.
    requirement: REQ-rf-g-40
    verification:
      - kind: integration
        ref: "src/AgroPlan.IntegrationTests/Api/AppliedFarmIntelligenceContractTests.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: Media and weekly-summary fixed-window limits return 429 at the configured thresholds and isolate farm partitions.
    verification:
      - kind: integration
        ref: "src/AgroPlan.IntegrationTests/Api/AppliedFarmIntelligenceContractTests.cs#Phase_four_media_and_summary_rate_limits_return_429_without_authorization_or_queueing"
        status: pass
    human_judgment: false
  - id: D4
    description: The AI-SPEC has 45 fictional metadata-only reference cases with offline xUnit coverage.
    verification:
      - kind: unit
        ref: "src/AgroPlan.UnitTests/AI/Phase04EvaluationTests.cs; Category=AiEval (5 passed)"
        status: pass
    human_judgment: false
  - id: D5
    description: Real speech-provider production readiness requires consented bilingual evidence and domain review before enabling external processing.
    requirement: REQ-rf-g-39
    verification: []
    human_judgment: true
    rationale: The repository has no selected speech provider or consented evaluation recordings; humans must approve provider compatibility, privacy terms, and the measured accuracy report.
metrics:
  duration: 18min
  completed: 2026-11-11
  status: complete
  commits: 5
  plan_head_before: 7ffdc92a56025e63d2b421f825a587d2cbbbf3e5
  plan_head_after: f64bafe
---

# Phase 4 Plan 04: Provider-gated Worker integration and evaluation summary

**Phase 4 workers now fail closed by provider operation, while OpenAPI, Scalar, limits, privacy logs, and 45 offline AI-SPEC cases have regression coverage.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-11-11T16:23:02Z
- **Completed:** 2026-11-11T16:40:44Z
- **Tasks:** 3
- **Files modified:** 15

## Accomplishments

- Added a single Worker registration gate for voice, weekly-summary narrative, and symptom observation consumers. Production does not register an unconfigured or unapproved operation; development/test adapters return typed unavailable outcomes without network calls. Speech additionally requires verified Dominican Spanish and Haitian Creole support, all 45 reference cases, at least 20 consented recordings per language plus code-switch evidence, and 95% exact field accuracy separately per language.
- Added cross-feature API contract checks for voice, weekly summary/share/source, symptom photo/review, and agronomist authorization routes. Development Scalar/OpenAPI loads; Production hides both. Media requests are limited to 10 per user/farm/hour and summary requests to 20, returning 429 without entering endpoint work after the threshold.
- Added five offline `Category=AiEval` assertions for 45 fictional metadata-only cases (25 voice, 10 summary, 10 symptom). A captured summary-provider failure log test confirms provider error text, user ID, and farm ID are not emitted.

## Task Commits

1. **Task 1: Provider-gated Worker registration tracer** - `10e298b` (RED tests), `718c8d7` (implementation)
2. **Task 2: Cross-feature API and privacy regression coverage** - `c68eced` (RED tests/evidence), `d3860df` (operation approval gates)
3. **Task 3: AI-SPEC offline reference fixtures and deterministic evaluations** - `f64bafe` (fixtures and tests)

## Files Created/Modified

- `src/AgroPlan.Worker/AppliedFarmIntelligenceWorkerRegistration.cs` - Default-off, operation-scoped activation and evidence gates.
- `src/AgroPlan.Worker/AI/UnconfiguredSpeechTranscriptionProvider.cs`, `UnconfiguredWeeklySummaryProvider.cs`, `UnconfiguredSymptomObservationProvider.cs` - Offline typed unavailable adapters.
- `src/AgroPlan.Worker/Program.cs` - Shared registration call and independent media cleanup workers.
- `src/AgroPlan.Worker/Jobs/WeeklySummaryConsumer.cs` and `src/AgroPlan.Application/Abstractions/IWeeklySummaryNarrativeProvider.cs` - Persist explicit unavailable status for unconfigured narrative provider.
- `src/AgroPlan.IntegrationTests/Worker/AppliedFarmIntelligenceWorkerActivationTests.cs` - Default-off and operation-approval registration tests.
- `src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs` - Provider-error log privacy assertion.
- `src/AgroPlan.IntegrationTests/Api/AppliedFarmIntelligenceContractTests.cs` - OpenAPI/Scalar, production visibility, and rate-limit contracts.
- `src/AgroPlan.UnitTests/AI/Phase04EvaluationCases.cs` and `Phase04EvaluationTests.cs` - 45 synthetic, offline reference cases and assertions.
- `.planning/debug/04-04-task1-red-evidence.json`, `.planning/debug/04-04-task2-red-evidence.json`, `.planning/debug/phase4-worker-activation-red.junit.xml` - Validated TDD RED evidence artifacts.

## Decisions Made

- Speech, narrative, and symptom provider operations require separate adapter and activation decisions; Gemini proposal handling does not implicitly activate any Phase 4 provider.
- Summary and symptom activation require a configured provider name, exact operation identifier (`narrative` or `observation`), and explicit approved-evidence configuration.
- Synthetic evaluation cases never substitute for the consented provider validation required before production speech activation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Functionality] Require approval evidence for summary and symptom providers**
- **Found during:** Task 2
- **Issue:** Initial registration only required a provider interface and enabled flag for weekly summary and symptom operations, allowing an unverified provider to be hosted in Production.
- **Fix:** Require provider name, exact operation match, and `EvidenceApproved=true`; added failing tests before implementation.
- **Files modified:** `src/AgroPlan.Worker/AppliedFarmIntelligenceWorkerRegistration.cs`, `src/AgroPlan.IntegrationTests/Worker/AppliedFarmIntelligenceWorkerActivationTests.cs`
- **Verification:** Two RED tests failed before the fix; focused integration suite then passed 10/10.
- **Committed in:** `c68eced`, `d3860df`

**2. [Rule 2 - Privacy Regression Coverage] Capture summary-provider logs for payload leakage**
- **Found during:** Task 2
- **Issue:** The plan required verifying that provider error content and farm/user identifiers do not leak into integration logs, but no captured-log regression existed for this workflow.
- **Fix:** Added a capturing logger test with a unique provider-error marker, user ID, and farm ID.
- **Files modified:** `src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs`
- **Verification:** Provider failure test passed in the focused integration suite and full solution suite.
- **Committed in:** `c68eced`

---

**Total deviations:** 2 auto-fixed (2 Rule 2 correctness/privacy coverage)
**Impact on plan:** Both changes close production activation and log privacy gaps directly related to the planned trust boundaries.

## Issues Encountered

- The first Scalar HTML assertion expected the OpenAPI URL to be embedded literally in the response body. Scalar serves a dynamically configured UI, so the test now verifies the contract paths through `/openapi/v1.json` and verifies `/scalar/` returns HTTP 200.
- The rate-limit regression initially used `GET` for weekly summaries, but the controller only maps `POST` on the collection path. The test uses the actual `POST` route; it then passed at the 20/21 request boundary.
- The test host stalled when run inside the restricted sandbox. Running the same .NET 10 commands with Docker/Testcontainers access completed normally.

## Verification

- RED evidence: `gsd-tools check tdd-red-evidence .planning/debug/04-04-task2-red-evidence.json --raw` → `RED_EVIDENCE_OK`.
- Focused Worker/API/privacy tests: 10 passed, 0 failed.
- Offline evaluation filter: `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter 'Category=AiEval' -m:1 -nr:false` → 5 passed; fixture manifest contains 45 cases.
- Full suite: `dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false` → 153 unit tests and 85 integration tests passed (238 total; 0 failed, 0 skipped). Existing xUnit1051 analyzer warnings remain in unrelated integration test files.

## Production Activation Gates

No new AI provider is selected. Speech, summary narrative, and automated symptom observation remain disabled until each has an explicitly configured provider, matching operation, and approved evidence. Speech also requires the bilingual consented evaluation and accuracy gates described in `COVERAGE.md`. Manual symptom review, deterministic weekly totals, and the existing Gemini proposal workflow remain separate.

## Next Phase Readiness

Plan 04-04 is complete and its tests pass. The shared `verify-phase-goal` step remains outstanding; do not mark Phase 4 complete until that verifier reports the phase goal achieved.

---
*Phase: 04-applied-farm-intelligence*
*Completed: 2026-11-11*

## Self-Check: PASSED

- Plan artifacts and all five plan commits exist in the current branch history.
- The summary records the measured plan commit count, before/after heads, test results, and known production activation gates.
