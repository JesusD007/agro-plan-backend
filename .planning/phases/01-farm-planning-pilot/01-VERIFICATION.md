---
phase: 01-farm-planning-pilot
verified: 2026-11-10T07:35:00Z
status: passed
score: 4/4 roadmap truths verified
covered_files:
  - .planning/phases/01-farm-planning-pilot/01-01-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-01-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-02-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-02-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-03-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-03-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-04-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-04-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-05-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-05-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-06-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-06-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-07-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-07-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-08-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-08-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-09-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-09-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-10-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-10-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-11-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-11-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/01-12-PLAN.md
  - .planning/phases/01-farm-planning-pilot/01-12-SUMMARY.md
  - .planning/phases/01-farm-planning-pilot/SECURITY.md
  - src/AgroPlan.Api/Controllers/CropCyclesController.cs
  - src/AgroPlan.Api/Controllers/FarmsController.cs
  - src/AgroPlan.Api/Controllers/LogbookController.cs
  - src/AgroPlan.Api/Controllers/PlanningJobsController.cs
  - src/AgroPlan.Api/Program.cs
  - src/AgroPlan.Application/Abstractions/IAiProposalProvider.cs
  - src/AgroPlan.Application/Planning/AiProposalDraftValidator.cs
  - src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj
  - src/AgroPlan.IntegrationTests/Api/CropCycleAndPlanningTests.cs
  - src/AgroPlan.IntegrationTests/Api/IdentityAndFarmAuthorizationTests.cs
  - src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs
  - src/AgroPlan.IntegrationTests/Api/OpenApiDocumentationTests.cs
  - src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs
  - src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj
  - src/AgroPlan.UnitTests/Planning/PlantingFrameCalculatorTests.cs
  - src/AgroPlan.UnitTests/Planning/AiProposalDraftValidatorTests.cs
  - src/AgroPlan.UnitTests/Planning/GeminiProposalResponseTests.cs
  - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
  - src/AgroPlan.Worker/Climate/UnconfiguredClimateProvider.cs
  - src/AgroPlan.Worker/Jobs/PlantingFrameConsumer.cs
  - src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs
  - src/AgroPlan.Worker/Program.cs
covered_digest: "v4:sha256:e3148024fa592d98f0b752b586d55ab29b4028a683f4c8921206369d05325416"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: gaps_found
  previous_score: 0/4
  gaps_closed:
    - "Planting-frame safe mode reports named missing inputs and returns no numeric capacity/options until geometry and rules are validated."
  gaps_remaining: []
  regressions: []
---

# Phase 1: Farm Planning Pilot — Verification Report

**Phase Goal:** A producer can securely create an account, set up a farm and crop cycle, use planting assistance, and keep a manual logbook.
**Verified:** 2026-11-10T07:35:00Z
**Status:** passed
**Re-verification:** Yes — after gap closure work on planting-frame safe mode and test infrastructure.

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|---|---|---|
| 1 | A producer can create and access an account, create a farm and parcels, and associate a crop cycle with a parcel; users outside that farm cannot read or change its records. | ✓ VERIFIED | `Producer_can_register_create_a_farm_and_parcel_then_read_them_over_http` exercises registration, authenticated farm and parcel POSTs, and readback. `Farm_membership_is_rechecked_on_each_request_and_scoped_to_that_farm` proves cross-farm denial, immediate revocation, and 403 responses when an Administrator attempts Owner-only grant/revoke operations; `CropCycleAndPlanningTests` proves same-farm cycle creation and foreign-parcel rejection. |
| 2 | A producer can request a sowing proposal, inspect climate availability or missing-data status plus its factors and uncertainty, and explicitly confirm only a complete, supported proposal; live climate and numeric agronomic advice stay disabled until sources are selected and validated. | ✓ VERIFIED | `OutboxAndConsumerTests.Outbox_publishes_jobs_and_workers_persist_safe_missing_data_without_calling_ai` submits a real HTTP job through PostgreSQL outbox and RabbitMQ worker, asserts safe `insufficient_data`, explicit missing fields, no AI call, no confirmation, and conflict on confirmation. No claim is made that Gemini or a live climate source was called. |
| 3 | A producer can request a farm-scoped planting-frame proposal. When validated spacing rules and usable parcel geometry are available, capacity is calculated deterministically and the producer explicitly confirms a choice; otherwise the API names the missing inputs and returns no capacity or options. Numeric comparison stays gated until agronomic sources and geometry are validated. | ✓ VERIFIED | Worker integration test confirms the current safe path returns empty options, names missing geometry/rules, disallows confirmation, and rejects confirm with 409. `PlantingFrameCalculatorTests` separately prove deterministic arithmetic for synthetic test-only validated data and rejection of missing, mismatched-region, or expired rules. Runtime agronomic values are not seeded. |
| 4 | An authorized producer can add and correct manual logbook entries while preserving the correction history. | ✓ VERIFIED | `LogbookAndAttachmentsTests.Corrections_append_history_and_database_rejects_mutating_logbook_rows` exercises correction through HTTP, verifies linked new record and preserved original, and checks PostgreSQL rejects update/delete. |

**Score:** 4/4 roadmap truths verified.

## Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| Account, farms, parcels, cycles, farm authorization | API and persistence paths | VERIFIED | Integration test registers a user, creates farm and parcel over HTTP, and reads both; separate test proves cross-farm denial and request-time membership revocation. |
| Crop catalog and crop cycles | Five approved crop identities with missing validated agronomy reported | VERIFIED | PostgreSQL integration asserts plátano, yuca, batata, arroz, habichuela; `IdentityOnly` status; missing parameters; and cycle creation. |
| Sowing proposal flow | HTTP submission, durable outbox, worker result, safe missing-data response | VERIFIED for safe mode | Real API/DB/Rabbit integration covers job publish/consume and invalid confirmation. AI provider is not called because inputs are incomplete. |
| Planting-frame flow | Deterministic validated calculator with safe missing-data API response | VERIFIED for current safe mode | No production frame rules or usable geometry are invented; API result contains missing inputs and no options/capacity. |
| Logbook and attachments | Farm-scoped corrections, immutable history and attachment handling | VERIFIED | Integration tests cover corrections and database immutability, plus attachment finalization and cross-farm denial using fake storage. |
| xUnit v3 test solution | Unit and isolated integration coverage for pilot | VERIFIED | Final solution run reported by orchestrator: 55 passed (46 unit, 9 PostgreSQL/RabbitMQ integration), 0 failed/skipped. Farm/parcel HTTP creation, provider-output validation, and raw Gemini JSON parsing are covered. |
| Provider-neutral AI and Scalar docs | Provider replaceability and interactive Development API docs | VERIFIED | `IAiProposalProvider` is the worker/application seam; DI registers `GeminiProposalProvider`. OpenAPI/Scalar integration test validates generated OpenAPI, bearer scheme, Scalar response, and no docs routes in Production. |
| Shared AI response validation | Every AI adapter result is checked before persistence | VERIFIED | `SowingPlanConsumer` runs `AiProposalDraftValidator.Validate(input, draft)` before serializing/persisting a provider response. Validator rejects unsupported numeric claims, unknown source IDs, malformed missing-input IDs, incomplete provenance and missing collections. `AiProposalDraftValidatorTests` contributes 18 xUnit cases. |
| Gemini raw response parser | Malformed or unsafe provider JSON cannot become a proposal | VERIFIED | `GeminiProposalResponseTests` covers missing/malformed JSON, absent summary/evidence, unsupported numeric claims, unknown sources, malformed missing-input identifiers, and valid Gemini provenance; parser output also passes the common validator. |

## Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| Farm-scoped controller actions | Active membership | Authorization policy → `IFarmAuthorization` → current database membership query | WIRED | Per-request membership integration test proves revocation is effective on the next request and cross-farm access is denied. |
| Crop-cycle API | Crop catalog and parcel persistence | EF Core query/save | WIRED | Integration test proves catalog identities, same-farm cycle create, and foreign-parcel reject. |
| Sowing API | Outbox → RabbitMQ → worker → persisted proposal | HTTP submit, publisher, consumer, PostgreSQL | WIRED + BEHAVIOR TESTED | Integration test observes published outbox, worker result, duplicate delivery protection, and safe missing-input result. |
| Worker | Provider-neutral AI contract | DI maps `IAiProposalProvider` to `GeminiProposalProvider` | WIRED | Worker consumes the generalized abstraction; integration worker test replaces it with a counting fake. No live Gemini call was made. |
| Gemini JSON response | Raw provider parser → shared validator → persistence boundary | `GeminiProposalProvider.ParseResponse` applies `AiProposalDraftValidator`; `SowingPlanConsumer` validates again before persistence | WIRED + UNIT TESTED | Raw parser tests cover malformed output and accepted provenance. The production Gemini API was not called. |
| Provider result | Shared AI validation boundary | `SowingPlanConsumer` calls `AiProposalDraftValidator.Validate` before storing a draft | WIRED | Same validation boundary applies to any `IAiProposalProvider` implementation; unit tests exercise valid and rejected outputs. |
| Climate adapter | Sowing result | `UnconfiguredClimateProvider` reports unavailable/missing | WIRED | Safe missing-input path is exercised. A live weather provider is not selected or enabled. |
| Frame worker | Calculator and result | `PlantingFrameConsumer` → `PlantingFrameCalculator` → persisted result | WIRED + BEHAVIOR TESTED | Current worker explicitly passes absent geometry; integration asserts named missing fields and no options. Unit tests cover deterministic calculation using synthetic fixture rules. |
| Logbook correction endpoint | Immutable correction chain | Controller → use case → PostgreSQL trigger | WIRED + BEHAVIOR TESTED | HTTP correction and database update/delete rejection are asserted by integration test. |
| OpenAPI document | Scalar UI | API OpenAPI route consumed by Scalar mapping in Development | WIRED + BEHAVIOR TESTED | Integration test checks JSON shape, route coverage, bearer security scheme, Scalar UI, and Production route absence. |

## Data-Flow Trace (Level 4)

| Artifact | Data | Source | Produces real data | Status |
|---|---|---|---|---|
| Farm, parcel, cycle, logbook endpoints | Tenant rows | EF Core/PostgreSQL | Yes | Source is DB-backed; registration-to-farm/parcel create/read is covered by PostgreSQL-backed HTTP integration. |
| Sowing proposal | Status, factors, missing inputs | PostgreSQL snapshot + climate adapter + optional AI provider | Yes in current safe mode; missing status from actual worker result | FLOWING; provider not invoked with current missing input. |
| Planting-frame proposal | Options and missing input list | Crop frameworks + parcel geometry + deterministic calculator | Real missing-data result; no current numeric capacity/options | FLOWING; options gated correctly. |
| Attachments | Upload URL and metadata | Fake storage in tests + PostgreSQL | Test-only signed URL/metadata | Test path flows; Supabase production storage not exercised. |
| API docs | OpenAPI paths and schema | ASP.NET Core OpenAPI generation | Yes | FLOWING; Scalar UI consumes document in Development. |

## Behavioral Spot-Checks

| Behavior | Command/evidence | Result | Status |
|---|---|---|---|
| Full solution test suite | Orchestrator-observed `dotnet test src/AgroPlan.sln --no-restore -m:1` | 55 passed: 46 unit, 9 PostgreSQL/RabbitMQ integration; 0 failed/skipped | PASS (reported run; not rerun during verification) |
| Account-to-farm setup | `Producer_can_register_create_a_farm_and_parcel_then_read_them_over_http` | Registration, HTTP farm/parcel creation, and readback succeeded | PASS (orchestrator reports this named test 1/1) |
| Provider response validation | `AiProposalDraftValidatorTests` (18 cases) | Valid input-source draft accepted; unsupported claims, unknown sources, malformed missing-input IDs, and incomplete metadata rejected | PASS (suite result reported by orchestrator) |
| Raw Gemini parser | `GeminiProposalResponseTests` (14 parser cases) | Missing/malformed JSON, invalid evidence, numeric claims, and invalid metadata rejected; valid source provenance retained | PASS (suite result reported by orchestrator) |
| Membership revocation, cross-farm denial, and role restrictions | `Farm_membership_is_rechecked_on_each_request_and_scoped_to_that_farm` | Same token denied after revocation; cross-farm request and Administrator Owner-only grant/revoke attempts return 403 | PASS |
| Safe sowing/frame jobs and duplicate delivery | `Outbox_publishes_jobs_and_workers_persist_safe_missing_data_without_calling_ai` | Outbox delivered, safe missing values persisted; AI not invoked; duplicate event did not reprocess; confirms rejected | PASS |
| Correction immutability | `Corrections_append_history_and_database_rejects_mutating_logbook_rows` | New linked correction persists; original unchanged; DB rejects update/delete | PASS |
| API documentation by environment | `Development_exposes_generated_openapi_and_interactive_scalar_reference`; `Production_does_not_map_api_documentation_routes` | Development OpenAPI + Scalar return usable content; Production returns 404 | PASS |

## Probe Execution

Not applicable: phase plans do not declare probe scripts and this is not a migration/tooling probe phase.

## Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
|---|---|---|---|
| REQ-pilot-accounts | 01-01, 01-02 | SATISFIED | Registration/login, farm and parcel setup, and support-mediated recovery exercised through API integration tests. |
| REQ-pilot-farms-parcels | 01-01, 01-03 | SATISFIED | Farm/parcel create/read tested over HTTP; tenant authorization/revocation is tested separately. |
| REQ-pilot-crop-cycle | 01-04 | SATISFIED | Catalog and same-farm cycle create/foreign-parcel rejection integration test. |
| REQ-pilot-sowing-plan | 01-05, 01-06 | SATISFIED for safe mode | Real async pipeline reports missing climate/geometry/agronomy and rejects incomplete confirmation; no live AI/climate used. |
| REQ-pilot-planting-frame | 01-07 | SATISFIED for safe mode | API reports missing geometry/rules and offers no options; deterministic calculator is covered with synthetic unit data. |
| REQ-pilot-manual-logbook | 01-08 | SATISFIED | Correction endpoint and DB immutable history tested; attachment finalization uses test fake. |
| REQ-pilot-provider-neutral-ai | 01-09, 01-11, 01-12 | SATISFIED | `IAiProposalProvider` DI seam wired to Gemini; Worker validates output with the provider-neutral validator before persistence; raw Gemini parser and shared validator rejection cases are covered. |
| REQ-pilot-api-docs | 01-09, 01-11 | SATISFIED | Development OpenAPI and Scalar endpoints tested; Production endpoints absent. |
| REQ-pilot-test-suite | 01-10, 01-11, 01-12 | SATISFIED | xUnit v3 solution test run passes 55 tests (46 unit, 9 integration); includes raw Gemini parsing, provider-output validation, role denials, and account-to-farm HTTP setup. |
| REQ-rf-g-01 | 01-03, 01-04 | SATISFIED | Farm/parcel setup, catalog/cycles and farm-scoped reads are covered over HTTP. |
| REQ-rf-g-05 | 01-08 | SATISFIED | Logbook correction and attachment behavior tested. |
| REQ-rf-g-10 | 01-05, 01-06 | SATISFIED for safe mode | Test proves missing information, no live climate, no AI call, and no incomplete confirmation. |
| REQ-rf-g-11 | 01-07 | SATISFIED for safe mode | No numeric comparison without validated inputs; deterministic unit fixture validates calculator when supported inputs exist. |
| REQ-rf-g-38 | 01-04, 01-07 | SATISFIED for safe mode | Approved crop identities and missing validated agronomic/frame data reported; no unsupported values invented. |

No additional Phase 1 mapped requirements outside plan requirement declarations were identified in the roadmap.

## Security Audit Status (Separate Gate)

The functional phase goal remains **passed** at 4/4 roadmap truths. The separate security audit in `SECURITY.md` is **OPEN_THREATS** and must be tracked as a security/release gate; it does not change this functional verification verdict. The audit reports 3 open high-severity threats and 2 open medium-severity threats:

- High T-01-10: bind confirmation to the exact proposal input and current agronomy, climate, and rule versions before enabling confirmable AI recommendations.
- High T-01-27: add integration coverage for migrating legacy attachment rows/references through immutable storage.
- High T-01-SC: document provenance and compatibility review for pinned direct/transitive production packages.
- Medium T-01-11: select climate source and enforce forecast freshness/expiry before enabling climate-backed recommendations.
- Medium T-01-17: verify private Supabase Storage bucket configuration before enabling photo storage.

These threats remain explicit release gates; safe-mode tests do not claim external provider or deployment configuration has been verified.

## Production Gates (Not Claimed as Verified)

- Supabase project, PostgreSQL production configuration, and migrations require environment provisioning.
- CloudAMQP production connection and broker configuration are unprovisioned; isolated RabbitMQ test coverage only proves the test integration.
- Gemini remains the current adapter behind the provider-neutral contract; no production API key/model call or provider quality was tested.
- No climate forecast source is selected. `UnconfiguredClimateProvider` intentionally returns unavailable data; select and validate a provider before production forecasts.
- No agronomic parameters, validated frame rules, or sufficient parcel geometry are available. Numeric capacity and option comparison remain gated as accepted.
- SMTP/support-role provisioning and Supabase private Storage bucket/configuration are required for production recovery and photo workflows; tests use fakes.

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---:|---|---|---|
| `src/AgroPlan.Worker/Program.cs` | 29 | `PlaceholderWorker` is registered only when RabbitMQ is unconfigured; actual consumers are registered when configured. | Info | Intentional idle mode; no functional worker runs until broker configuration is supplied. |

No unreferenced TBD/FIXME/XXX debt markers found in the reviewed implementation files. Test fixtures use explicitly synthetic values and fakes; no evidence they leak into runtime seeds.

## Human Verification Required

No manual UI or live-provider action is needed to verify the accepted Phase 1 safe mode. Production service integrations remain explicit provisioning gates above.

## Gaps Summary

The previous numeric planting-frame gap is closed by the roadmap's accepted safe-mode wording and behavior tests: without validated geometry and agronomic rules, the API names missing inputs, returns no capacity/options, and rejects confirmation. The API/worker, provider-neutral AI seam, shared and Gemini-specific output validation, Scalar/OpenAPI, logbook correction, current membership and role checks, account-to-farm setup, and remaining safe-mode flows are wired and covered by the reported 55-test solution run. Functional verification is passed. The separate security audit remains OPEN_THREATS with the release gates above; production Gemini, climate, Supabase, CloudAMQP, SMTP, and agronomic provisioning remain untested and disabled.

---

_Verified: 2026-11-10T07:35:00Z_
_Verifier: the agent (gsd-verifier)_
