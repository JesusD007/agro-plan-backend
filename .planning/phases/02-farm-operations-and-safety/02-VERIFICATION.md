---
phase: 02-farm-operations-and-safety
verified: 2026-11-10T16:47:42Z
status: passed
score: 7/7 must-haves verified
covered_files:
  - .planning/phases/02-farm-operations-and-safety/02-01-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-01-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-02-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-02-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-03-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-03-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-04-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-04-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-05-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-05-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-06-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-06-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-07-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-07-SUMMARY.md
  - .planning/phases/02-farm-operations-and-safety/02-08-PLAN.md
  - .planning/phases/02-farm-operations-and-safety/02-08-SUMMARY.md
  - src/AgroPlan.Api/Authorization/AdvisorReportAuthorizationHandler.cs
  - src/AgroPlan.Api/Authorization/AdvisorReportRequirement.cs
  - src/AgroPlan.Api/Controllers/AdvisorAccessController.cs
  - src/AgroPlan.Api/Controllers/AdvisorReportsController.cs
  - src/AgroPlan.Api/Controllers/CropCalendarsController.cs
  - src/AgroPlan.Api/Controllers/ExpenseEntriesController.cs
  - src/AgroPlan.Api/Controllers/HarvestRecordsController.cs
  - src/AgroPlan.Api/Controllers/InputApplicationsController.cs
  - src/AgroPlan.Api/Controllers/IrrigationEntriesController.cs
  - src/AgroPlan.Api/Controllers/OperationsReportsController.cs
  - src/AgroPlan.Api/Controllers/ReferencePricesController.cs
  - src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs
  - src/AgroPlan.Api/Controllers/WeatherAdvisoriesController.cs
  - src/AgroPlan.Api/Program.cs
  - src/AgroPlan.Api/Reports/CsvReportWriter.cs
  - src/AgroPlan.Application/Abstractions/IAiProposalProvider.cs
  - src/AgroPlan.Application/Abstractions/IReferencePriceProvider.cs
  - src/AgroPlan.Application/Abstractions/IWeatherAdvisoryProvider.cs
  - src/AgroPlan.Application/AdvisoryAccess/AdvisorGrantService.cs
  - src/AgroPlan.Application/Calendars/CalendarExplanationDraftValidator.cs
  - src/AgroPlan.Application/Calendars/CropCalendarEvaluator.cs
  - src/AgroPlan.Application/Reports/CostReport.cs
  - src/AgroPlan.Application/Reports/OperationsReportReader.cs
  - src/AgroPlan.Application/Reports/ProductionReport.cs
  - src/AgroPlan.Application/Safety/HarvestEligibilityEvaluator.cs
  - src/AgroPlan.Application/UseCases/AppendExpenseEntry.cs
  - src/AgroPlan.Application/UseCases/CorrectExpenseEntry.cs
  - src/AgroPlan.Domain/AdvisoryAccess/AdvisorAccessGrant.cs
  - src/AgroPlan.Domain/Calendars/CropCalendarTask.cs
  - src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs
  - src/AgroPlan.Domain/Operations/ExpenseEntry.cs
  - src/AgroPlan.Domain/Operations/HarvestEntry.cs
  - src/AgroPlan.Domain/Operations/InputApplicationEntry.cs
  - src/AgroPlan.Domain/Operations/IrrigationEntry.cs
  - src/AgroPlan.Domain/Safety/ValidatedProductLabel.cs
  - src/AgroPlan.Infrastructure/Climate/UnavailableWeatherAdvisoryProvider.cs
  - src/AgroPlan.Infrastructure/Messaging/Events/ScheduleChangeRequested.cs
  - src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs
  - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
  - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/AdvisorAccessGrantConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/CropCalendarConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/ExpenseEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/HarvestEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/InputApplicationEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/IrrigationEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/ValidatedProductLabelConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/AdvisorAccessGrants.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/CropCalendars.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/ExpenseEntries.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/HarvestSafety.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/InputApplications.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/IrrigationEntries.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/ScheduleChangeProposals.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/ScheduleProposalProcessingLease.cs
  - src/AgroPlan.Infrastructure/Prices/UnavailableReferencePriceProvider.cs
  - src/AgroPlan.IntegrationTests/Api/AdvisorAccessTests.cs
  - src/AgroPlan.IntegrationTests/Api/AdvisoriesTests.cs
  - src/AgroPlan.IntegrationTests/Api/CropCalendarTests.cs
  - src/AgroPlan.IntegrationTests/Api/ExpenseEntriesTests.cs
  - src/AgroPlan.IntegrationTests/Api/HarvestSafetyTests.cs
  - src/AgroPlan.IntegrationTests/Api/InputApplicationsTests.cs
  - src/AgroPlan.IntegrationTests/Api/IrrigationEntriesTests.cs
  - src/AgroPlan.IntegrationTests/Api/OperationsReportsTests.cs
  - src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs
  - src/AgroPlan.UnitTests/Advisories/AdvisoryFreshnessTests.cs
  - src/AgroPlan.UnitTests/AdvisoryAccess/AdvisorGrantTests.cs
  - src/AgroPlan.UnitTests/Calendars/CalendarExplanationValidationTests.cs
  - src/AgroPlan.UnitTests/Calendars/CropCalendarEvaluatorTests.cs
  - src/AgroPlan.UnitTests/Operations/ExpenseEntryTests.cs
  - src/AgroPlan.UnitTests/Operations/InputApplicationEntryTests.cs
  - src/AgroPlan.UnitTests/Operations/IrrigationEntryTests.cs
  - src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs
  - src/AgroPlan.UnitTests/Safety/HarvestEligibilityEvaluatorTests.cs
  - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
  - src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs
  - src/AgroPlan.Worker/Program.cs
covered_digest: "v4:sha256:6507bf4bf097fa6ce3da37457bc14854dfea0bf0609405ed80b1563f1410bec5"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 4/7
  gaps_closed:
    - "Crop-calendar progress maps each operation to at most one matching task."
    - "Date-filtered owner/advisor CSVs retain correction status across the full farm/cycle record set."
    - "Schedule proposals use current active planting evidence after a linked correction."
    - "Production reports use active planting records after linked sowing corrections."
    - "Expired Worker processing claims reach a terminal Stale state even after proposal expiry."
    - "Gemini receives no farm, template, sowing, task-key or source-version identifiers."
  gaps_remaining: []
  regressions: []
advisory: []
decision_coverage:
  honored: 12
  total: 12
  not_honored: []
---

# Phase 2: Farm Operations and Safety Verification Report

**Phase Goal:** A producer can maintain operational records and use them to make safer, better-informed farm decisions.
**Verified:** 2026-11-10T16:47:42Z
**Status:** passed
**Re-verification:** Yes — after review fixes.

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|---|---|---|
| 1 | Producers can append dated, farm/cycle-scoped expense, labor, material, irrigation, input-application and harvest records; corrections preserve originals and link actor/time/reason. | ✓ VERIFIED | Controllers apply farm-membership policy and farm-filter queries; migrations define same-farm composite references and immutable UPDATE/DELETE triggers. PostgreSQL integration tests cover farm isolation, corrections and mutation rejection (`ExpenseEntriesTests`, `IrrigationEntriesTests`, `InputApplicationsTests`, `HarvestSafetyTests`). |
| 2 | Frequency assessments use an exact current validated product/formulation/crop/use rule, otherwise return `unavailable`; harvest recording blocks on missing, stale, ambiguous or mismatched validated PHI evidence. | ✓ VERIFIED | `ApplicationFrequencyEvaluator` exact-matches keys and validity; `HarvestEligibilityEvaluator` returns blocked on missing exact labels/PHI and keeps MRL/re-entry separate. Unit tests exercise unknown/stale/mismatched rule and PHI cases; `HarvestSafetyTests.Missing_phi_blocks_harvest_and_persists_no_row` checks the API and persisted row count. |
| 3 | Crop calendars and harvest windows are traceable to validated templates and confirmed planting/progress, or explicitly report missing inputs; estimates never authorize actual harvest. | ✓ VERIFIED | `CropCalendarEvaluator` consumes each matched progress event at most once; `One_operation_completes_at_most_one_task_of_the_same_kind` passes. Sourced/missing-data estimates remain gated by current template, planting, progress and separate PHI checks. |
| 4 | Cost/production reports retain source currencies and units, expose source row IDs, avoid incompatible aggregation, and identify when cost per quintal cannot be computed safely. | ✓ VERIFIED | JSON and CSV retain source IDs and unit/currency grouping; correction IDs are resolved over the full farm/cycle record set before date filtering. Production reports select active planting evidence after corrections. Named CSV and production-report integration tests pass. |
| 5 | Weather and reference-price APIs return source/freshness-aware results; absent configured sources return unavailable rather than fabricated conditions or prices. | ✓ VERIFIED | Controllers require farm membership and provider contracts; default providers explicitly return `weather_source_not_configured` / `reference_price_source_not_configured`. Freshness and unavailable behavior have unit/API tests. Live providers remain a production gate by user decision. |
| 6 | Advisors can read only owner-granted cost/production scopes for one farm during the grant window; current expiry/revocation/scope is checked per request, with immediate revocation and no write scope. | ✓ VERIFIED | Grant domain limits grants to 90 days and read-only enum scopes; owner-controlled endpoint creates/revokes. Authorization handler reloads active unrevoked grants from PostgreSQL on each authorization; report endpoints authorize before queries. Integration tests cover allowed/denied scope, expiry and revocation. |
| 7 | Calendar-change proposals are deterministic and versioned; Gemini supplies only a bounded advisory explanation through `IAiProposalProvider` without receiving internal identifiers; applying dates requires producer confirmation of the current version. | ✓ VERIFIED | The Worker maps deltas to positional task labels, uses a generic rule-version label and semantic evidence labels; template/sowing GUIDs, source version and task keys remain server-side. The named PostgreSQL/RabbitMQ test asserts redaction and confirms that producer confirmation still applies only deterministic deltas. |

**Score:** 7/7 observable truths verified.

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `src/AgroPlan.Api/Controllers/ExpenseEntriesController.cs` | Farm-scoped records and corrections | ✓ VERIFIED | Buildable controller, policy-protected routes, farm-filtered reads and same-farm parcel/cycle validation. |
| `src/AgroPlan.Api/Controllers/HarvestRecordsController.cs` | PHI-gated harvest API | ✓ VERIFIED | Eligibility is evaluated before persistence; ineligible assessment returns conflict without saving. |
| `src/AgroPlan.Application/Calendars/CropCalendarEvaluator.cs` | Validated calendar and harvest-window evaluation | ✓ VERIFIED | Matching progress events are consumed once; regression test covers two same-kind tasks with one operation. |
| `src/AgroPlan.Api/Controllers/OperationsReportsController.cs` | Correct date-filtered cost CSV | ✓ VERIFIED | Correction status is calculated across unfiltered farm/cycle rows before date-window projection; owner and advisor CSV regression passes. |
| `src/AgroPlan.Api/Authorization/AdvisorReportAuthorizationHandler.cs` | Live advisor grant check | ✓ VERIFIED | Reads grant state from the database during each authorization and updates last-used metadata. |
| `src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs` | Durable bounded AI explanation worker | ✓ VERIFIED | Expired proposal claims transition to `Stale`; active expired leases are reclaimed with bounded attempts. Before calling the provider, it replaces internal IDs/keys/source versions with generic labels. The named PostgreSQL/RabbitMQ regression passes. |

### Key Link Verification

All GSD-registered links for plans 02-01 through 02-08 verified (16/16): API to domain validation, EF configuration to migration constraints, provider injection, farm-scoped evaluator/report wiring, live grant authorization, worker provider contract, and proposal confirmation wiring. `verify.key-links` returned `all_verified: true` for every plan.

### Data-Flow Trace (Level 4)

| Artifact | Data source | Flow | Status |
|---|---|---|---|
| Expense/application/irrigation/harvest APIs | PostgreSQL entries and current farm membership | Request → domain validation → EF save; list/report queries filter farm ID | ✓ FLOWING |
| Crop calendar | PostgreSQL crop cycle, logbook, operations, validated template and confirmed schedule rows | Farm/cycle query → active corrections filtered → evaluator → sourced output/missing codes | ✓ FLOWING |
| Cost and production report | PostgreSQL operation/harvest/cycle/parcel/logbook records | Scoped DB query → deterministic projection with currency/unit/source IDs and active planting-date projection → JSON/CSV; correction-state calculation precedes date filtering | ✓ FLOWING |
| Weather/reference price | Injected provider contracts | Default provider → explicit unavailable result until selected source is configured | ✓ FLOWING (safe unavailable) |
| Advisor reports | PostgreSQL grant + report rows | Per-request authorization → current scope/expiry/revocation check → scoped report query | ✓ FLOWING |
| AI schedule explanation | PostgreSQL outbox/proposal/current template + `IAiProposalProvider` | Durable request → Worker reloads/version-checks evidence → sends sanitized task/date changes and semantic evidence labels → explanation validation → producer confirmation applies deterministic delta; interrupted/expired claims reach recoverable or terminal states | ✓ FLOWING (fake-provider integration); Gemini-boundary redaction asserted; no live Gemini call |

### Behavioral Spot-Checks

| Behavior | Evidence | Result | Status |
|---|---|---|---|
| Unknown PHI prevents harvest persistence | `HarvestSafetyTests.Missing_phi_blocks_harvest_and_persists_no_row`; executor recorded focused PostgreSQL API tests passing | HTTP conflict, blocked status, missing evidence reason, zero harvest rows | ✓ PASS |
| Cost and production projections preserve units/currencies and exclude corrected source rows | `OperationsReportTests` plus `OperationsReportsTests`; executor recorded 81 unit / 23 integration tests passing at plan 02-06 | Assertions check DOP/USD grouping, kg/sack grouping, source IDs and `not_computable` state | ✓ PASS |
| Calendar proposals require confirmation and are projected only after it | `OutboxAndConsumerTests.Schedule_change_is_durable_explained_once_and_requires_producer_confirmation`; executor recorded focused test passing | Outbox/consumer/confirmation test passed with fake provider | ✓ PASS |
| One irrigation event does not complete multiple same-kind tasks | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~One_operation_completes_at_most_one_task_of_the_same_kind"` | 1 passed | ✓ PASS |
| Corrected expenses stay inactive in date-filtered owner/advisor CSV | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Date_filtered_owner_and_advisor_csv_keep_superseded_expense_inactive"` | 1 passed | ✓ PASS |
| Corrected planting remains confirmable | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Schedule_change_is_durable_explained_once_and_requires_producer_confirmation"` | 1 passed; proposal uses active planting correction and requires current producer confirmation | ✓ PASS |
| Expired Processing proposal becomes Stale after delivery | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Expired_processing_schedule_proposal_becomes_stale_on_redelivery"` | 1 passed | ✓ PASS |
| Gemini-boundary redaction and deterministic confirmation | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Schedule_change_is_durable_explained_once_and_requires_producer_confirmation"` | 1 passed; serialized provider input excludes template/sowing GUIDs, internal source version and task key; producer confirmation succeeds | ✓ PASS |
| Final aggregate test execution | `.tools/dotnet/dotnet.exe test src/AgroPlan.sln --no-restore -m:1` (orchestrator, post-fix, outside sandbox) | 93 unit + 28 integration tests passed; 0 failures/skips. NuGet vulnerability feed emitted NU1900 because nuget.org was unreachable. | ✓ PASS |

### Test Quality Audit

| Test File Group | Linked Requirements | Active/Skipped | Circular | Assertion Strength | Verdict |
|---|---|---|---|---|---|
| Phase 2 xUnit v3 domain tests (`Operations`, `Safety`, `Calendars`, `Reports`, `Advisories`, `AdvisoryAccess`) | REQ-rf-g-02/03/04/07/08/13/15/32/33/34/35/37/42 | No disabled tests found | No output-generating test fixtures found | Value and behavior assertions | PASS |
| Phase 2 Testcontainers API/Worker tests | REQ-rf-g-02/07/08/13/15/31/32/35/36/37/42 | No disabled tests found | Tests seed independent input fixtures and assert persisted/API results | Multi-step HTTP/DB assertions | PASS |

No disabled requirement-linked tests or circular expected-value generation were found. The post-fix full solution run passed 93 unit and 28 integration tests; the only warning was unavailable NuGet vulnerability metadata (`NU1900`).

### Decision Coverage

12/12 trackable Phase 2 context decisions are represented in plans and implementation, including fail-closed PHI, immutable linked corrections, scoped/revocable advisor access, explicit unit/currency provenance, unavailable climate/prices, and provider-neutral non-authoritative AI.

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence / limitation |
|---|---|---|---|
| REQ-rf-g-02 | 02-01 | SATISFIED | Farm-scoped expense/labor/material append and immutable correction tests. |
| REQ-rf-g-03 | 02-06 | SAFE MODE / PRODUCTION GATE | Currency/unit groups and missing conversion inputs are reported; cost per quintal remains `not_computable` until a validated crop-specific quintal definition and source-backed conversion are loaded. |
| REQ-rf-g-04 | 02-05 | SAFE MODE / PRODUCTION GATE | Validated-source calendar path exists; one progress event completes at most one task. No template is seeded, so recommendations remain unavailable until trusted rules are loaded. |
| REQ-rf-g-07 | 02-04 | SATISFIED | Missing, stale, ambiguous or mismatched PHI blocks harvest; exact-label evidence is persisted. |
| REQ-rf-g-08 | 02-03 | SAFE MODE / PRODUCTION GATE | Price endpoint reports explicit unavailable until an approved feed is selected. |
| REQ-rf-g-13 | 02-05, 02-08 | SATISFIED | Deterministic proposal/confirmation and AI grounding are covered; active corrected sowing is used, processing claims are bounded/recoverable, and proposals that expire while Processing transition to Stale. |
| REQ-rf-g-15 | 02-06 | SATISFIED | JSON/CSV reporting retains currency/unit provenance and filtered CSV correction state agrees with JSON even when the correction is outside the date range. |
| REQ-rf-g-31 | 02-02 | SATISFIED | Structured irrigation record, farm scope, units, and correction coverage. |
| REQ-rf-g-32 | 02-02, 02-04 | SATISFIED | Structured applications, frequency status with unavailable on missing rules, and PHI-gated harvest. |
| REQ-rf-g-33 | 02-05 | SAFE MODE / PRODUCTION GATE | Stage guidance remains unavailable until validated crop-stage rules are loaded; no fertilizer rates are invented. |
| REQ-rf-g-34 | 02-05 | SAFE MODE / PRODUCTION GATE | Calendar reminders use validated templates when supplied; progress events complete one matching task each. No templates are seeded. |
| REQ-rf-g-35 | 02-03 | SAFE MODE / PRODUCTION GATE | Weather API returns source/freshness-aware unavailable status until a feed is selected. |
| REQ-rf-g-36 | 02-06 | SATISFIED | Production report/CSV includes crop, area, sowing date and actual harvested quantities with units/source IDs. |
| REQ-rf-g-37 | 02-07 | SATISFIED | Read-only cost/production grants are farm/category/time scoped, revocable and checked per request. |
| REQ-rf-g-42 | 02-04, 02-05 | SATISFIED | Traceable harvest windows/actions require validated inputs; missing evidence is explicit and actual harvest remains PHI-gated. |

### Advisory (New Scope, Unevidenced)

None. Re-verification produced no unevidenced new-scope blocker.

### Gaps Summary

All carried-forward review findings are closed and covered by named regression tests. The final suite and goal-backward checks show no remaining Phase 2 implementation gaps. External weather/price sources, validated agronomic records, and live Gemini activation remain production prerequisites, represented by explicit unavailable/safe-mode behavior rather than invented data.

### Production Gates

- Climate and market-price feeds are unselected; APIs intentionally return unavailable with no synthetic values.
- No crop-calendar templates, product-label records, frequency rules, or validated quintal/unit conversion rules are seeded. Calendar recommendations and cost-per-quintal stay unavailable until trusted source records are reviewed and loaded.
- The AI activation dataset, approved credentials, model, privacy/retention review, monitoring and kill switch remain prerequisites in `02-AI-SPEC.md`; no live Gemini call was made.
- GSD security audit returned `SECURED`: 23/23 threat entries closed, including T-02-17 identifier redaction at the Gemini boundary. The test verifies the redaction on the provider contract without making a live provider call.
- Post-fix solution-wide xUnit v3 tests passed (93 unit + 28 integration, no failures or skips) when run outside the sandbox. NU1900 only indicates NuGet vulnerability metadata could not be reached; it did not prevent restore/build/test execution.

---

_Verified: 2026-11-10T16:47:42Z_  
_Verifier: gsd-verifier_
