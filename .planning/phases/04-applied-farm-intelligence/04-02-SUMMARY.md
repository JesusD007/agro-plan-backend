---
phase: 04-applied-farm-intelligence
plan: 02
subsystem: api
tags: [aspnet-core, postgresql, xunit-v3, testcontainers, scalar, weekly-summary, advisor-grants]
requires:
  - phase: 02-farm-operations-and-safety
    provides: Deterministic operations reporting and live advisor report grants
provides:
  - Farm-local, request-driven weekly summaries with deterministic source and total projections
  - Scope-filtered advisor summary and source reads with live grant checks
  - Provider-neutral bounded narrative contract and optional outbox Worker consumer
affects: [04-03, 04-04, advisor-reports, weekly-summaries]
tech-stack:
  added: []
  patterns: [farm-time-zone-week-window, deterministic-jsonb-snapshot, optional-provider-outbox, per-job-opaque-source-aliases]
key-files:
  created:
    - src/AgroPlan.Api/Controllers/WeeklySummariesController.cs
    - src/AgroPlan.Application/Abstractions/IWeeklySummaryNarrativeProvider.cs
    - src/AgroPlan.Application/Reports/WeeklySummaryProjection.cs
    - src/AgroPlan.Domain/Reports/WeeklySummary.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/WeeklySummaryConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/WeeklySummaries.cs
    - src/AgroPlan.Worker/Jobs/WeeklySummaryConsumer.cs
    - src/AgroPlan.UnitTests/Reports/WeeklySummaryProjectionTests.cs
    - src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs
    - src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Worker/Program.cs
key-decisions:
  - "Resolve the requested date to its containing Monday-through-Sunday week in the configured farm time zone."
  - "Use confirmed, non-superseded records for totals; report pending tasks and missing data separately, and fail closed above 500 rows."
  - "Keep narrative optional and provider-neutral; do not enqueue work when no provider is registered."
  - "Send only bounded projection data and randomized job-local aliases to a narrative provider; map validated aliases back to source IDs on the server."
  - "Share only with a current Costs or Production advisor grant; recheck grant scope for each summary and source read and omit pending tasks."
  - "Treat the request date as an implementation default selecting its containing local calendar week."
patterns-established:
  - "Projection arithmetic and source selection remain deterministic and independent from narrative generation."
  - "Revalidate farm membership and advisor grants from PostgreSQL at each protected operation."
requirements-completed: [REQ-rf-g-40]
coverage:
  - id: D1
    description: Farm-local deterministic projection keeps confirmed sources, totals, pending tasks, and missing data separate.
    requirement: REQ-rf-g-40
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Reports/WeeklySummaryProjectionTests.cs#WeeklySummaryProjectionTests
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs#Owner_can_request_a_farm_local_weekly_summary
        status: pass
    human_judgment: false
  - id: D2
    description: Owner/admin requests and advisor shares enforce farm membership, live grant scope, filtered source reads, and rate limits.
    requirement: REQ-rf-g-40
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs#Owner_can_share_summary_with_current_cost_grant_and_revocation_denies_the_next_read
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs#Summary_rate_limit_is_partitioned_per_user_and_farm
        status: pass
    human_judgment: false
  - id: D3
    description: Optional narrative jobs enforce 64 KiB input and 16 KiB output limits, reject unknown aliases and numeric claims, and recheck requester membership.
    requirement: REQ-rf-g-40
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs#WeeklySummaryConsumerTests
        status: pass
    human_judgment: false
  - id: D4
    description: Development Scalar/OpenAPI exposes weekly summary routes while Production hides the documentation.
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs#WeeklySummaryOpenApiTests
        status: pass
    human_judgment: false
actuals:
  tokens: 22458
  tasks: 2
  commits: 4
commits: 4
plan_head_before: 777eb1529657db44822d7371a2cfb86761901cb2
plan_head_after: 65ca9f1396939dd4d7c40a12472c334e4f1cada0
metrics:
  duration: 49min
  completed: 2026-11-11
status: complete
---

# Phase 4 Plan 2: Weekly Farm Summaries Summary

**Farm-local weekly projections now preserve traceable totals and scopes, with bounded narrative generation behind an optional provider port.**

## Performance

- **Duration:** 49 minutes
- **Started:** 2026-11-11T14:33:35Z
- **Completed:** 2026-11-11T15:22:33Z
- **Tasks:** 2/2
- **Files changed:** 13

## Accomplishments

- Added request-driven Monday-through-Sunday summaries in farm-local time, with crop-cycle filtering, confirmed source rows, separate pending tasks and missing-data markers, original units/currencies, and a hard 500-row limit.
- Added owner/admin authorization, a per-user/farm intake limit, scope-compatible advisor sharing, per-request grant and membership rechecks, and individually filtered source reads.
- Added an optional neutral narrative port and Worker job that sends at most 64 KiB of bounded projection fields with random aliases, rejects oversized or unsupported output, and maps validated aliases to source IDs server-side.
- Added Scalar/OpenAPI coverage; API and Worker tests run against PostgreSQL Testcontainers.

## Task Commits

1. **Task 1: On-demand deterministic farm-week summary tracer** — `9d15521` (test RED), `f271ece` (implementation)
2. **Task 2: Bounded prose job and compatible advisor summary/source reads** — `65ca9f1` (implementation and Worker tests)

The measured range also contains `421b342`, a separate 04-01 voice request-validation fix needed to restore the phase-wide integration suite. The final plan metadata commit records this summary and state updates.

## Files Created/Modified

- `src/AgroPlan.Application/Reports/WeeklySummaryProjection.cs` — deterministic week window, totals, source references, pending tasks, and missing-data projection.
- `src/AgroPlan.Domain/Reports/WeeklySummary.cs` — persisted projection snapshot, narrative state, validated narrative source IDs, and share metadata.
- `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs` — create/read/share/source endpoints with current authorization and scope filtering.
- `src/AgroPlan.Worker/Jobs/WeeklySummaryConsumer.cs` — bounded optional narrative job, alias validation, authorization rechecks, and safe publication.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/WeeklySummaries.cs` — PostgreSQL table, constraints, foreign keys, and indexes.
- `src/AgroPlan.UnitTests/Reports/WeeklySummaryProjectionTests.cs` — local week, DST, corrections, units/currencies, rejected tasks, and row ceiling coverage.
- `src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs` and `src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs` — API, grant, rate-limit, provider, and Scalar/OpenAPI tests.

## Decisions Made

- A requested anchor date selects its containing calendar week in the farm time zone. The date-to-week interpretation remains an implementation default as noted in the plan.
- Advisors with an active compatible Costs or Production grant see only authorized totals and source rows. Pending task details stay owner/admin-only because the grant model has no task scope.
- Narrative is unavailable without a registered provider. In that state the API creates no outbound narrative event.
- Model text cannot author numeric totals; provider input/output size limits are enforced without truncating deterministic data.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Excluded rejected tasks from the pending-task projection**
- **Found during:** Task 1
- **Issue:** Filtering only for non-Verified tasks incorrectly included Rejected tasks as pending.
- **Fix:** Limit pending rows to Pending, InProgress, Submitted, and CorrectionRequested states.
- **Files modified:** `src/AgroPlan.Application/Reports/WeeklySummaryProjection.cs`, `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs`
- **Verification:** Projection and full solution tests pass.
- **Committed in:** `f271ece`

**2. [Rule 2 - Missing Critical] Registered and correctly ordered per-user/per-farm rate limiting**
- **Found during:** Task 1
- **Issue:** The plan placed limiter registration in later Plan 04-04, but this controller needed a registered policy now; the middleware was also before authentication and would partition requests under an unauthenticated identity.
- **Fix:** Register `phase4-summary` in the API, run rate limiting after authentication, and use HTTP 429 for rejected requests.
- **Files modified:** `src/AgroPlan.Api/Program.cs`
- **Verification:** Integration test confirms 20 requests for one user/farm are allowed, the next is rate-limited, and another member retains a separate quota.
- **Committed in:** `f271ece`

---

**Total deviations:** 2 auto-fixed (1 Rule 1, 1 Rule 2)
**Impact on plan:** Both changes were required for correct task state and the planned rate-limit contract.

## TDD Gate Compliance

- **RED:** `9d15521` added the PostgreSQL/API contract first. The owner request returned 404 instead of the expected 201; GSD classified the preserved VSTest TRX evidence as `RED_EVIDENCE_OK` using a local TRX-to-JUnit adapter.
- **GREEN:** The focused projection tests passed 4/4, and the final focused API/Worker/Scalar suite passed 11/11.
- **REFACTOR:** Fixed the rate-limit middleware identity ordering and rejected-task selection; reran focused tests and the full solution successfully.
- **No skipped tests or unrun plan verification remain.**

## Verification

- `dotnet build src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 -nr:false` — passed, 0 errors. The build reported existing xUnit analyzer warnings in integration tests.
- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-build --no-restore --filter "FullyQualifiedName~WeeklySummaryProjectionTests" -m:1 -nr:false` — passed, 4/4.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~WeeklySummary" -m:1 -nr:false` — passed, 11/11.
- `dotnet test src/AgroPlan.sln --no-build --no-restore -m:1 -nr:false` — passed, 211/211 (143 unit and 68 integration; no skips; PostgreSQL Testcontainers active).

## Issues Encountered

- The first local VSTest attempt timed out waiting for testhost. Rerunning through the elevated Windows test environment succeeded; the final full solution run passed.
- The first full integration run surfaced an unrelated 04-01 record-validation metadata error in `CreateVoiceUploadAuthorizationRequest`. It was fixed separately in `421b342`; voice integration tests and the subsequent full solution run passed.

## User Setup Required

None. No narrative provider is registered, so prose remains unavailable and no provider request is emitted until a provider is configured.

## Next Phase Readiness

Plan 04-02 is complete. Phase 4 Plan 04-03 can proceed. Weekly summaries retain their deterministic projection when narrative is unavailable, and advisor access remains limited by current Costs/Production grants.

## Self-Check: PASSED

- Summary file exists at the required phase path.
- RED, Task 1, and Task 2 commits (`9d15521`, `f271ece`, `65ca9f1`) are ancestors of the current branch HEAD.
- `ROADMAP.md` now records Phase 4 at 2/4 plans in progress.

---
*Phase: 04-applied-farm-intelligence*
*Completed: 2026-11-11*
