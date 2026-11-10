---
phase: 03-workforce-and-offline-operations
plan: 07
subsystem: api
tags: [aspnetcore, xunit-v3, scalar, openapi, postgres, rabbitmq, offline-sync]

# Dependency graph
requires:
  - phase: 03-workforce-and-offline-operations
    provides: worker invitations, task/evidence lifecycle, offline sync, rewards, routes, and schedule/calendar revisions
provides:
  - API and Worker dependency-injection registration for phase 3 consumers and safe calendar provider
  - Cross-feature API regression coverage for invited worker evidence, correction, wage, and progress lifecycle
  - Development Scalar/OpenAPI workforce endpoint checks and Production docs-hidden checks
affects: [phase-03-verification, phase-04-applied-farm-intelligence]

# Actuals (#2632)
actuals:
  tokens: 4475
  tasks: 2
  commits: 2
plan_head_before: f0bf0d7baf1f25a33ed0eec719b82f61d25da18b
plan_head_after: 4c34b84b364fea79396289f65f5dc08d842de30a

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Provider-neutral calendar adapter remains retryable while no external calendar provider is configured.
    - Phase integration tests exercise API lifecycle contracts against PostgreSQL Testcontainers.

key-files:
  created:
    - src/AgroPlan.IntegrationTests/Api/WorkforceOfflineEndToEndTests.cs
    - src/AgroPlan.IntegrationTests/Api/WorkforceOpenApiTests.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Worker/Program.cs
    - src/AgroPlan.IntegrationTests/IntegrationSupport.cs

key-decisions:
  - "Use the existing per-feature EF migrations from Plans 03-02 through 03-06; do not add an empty/consolidated migration when all schema changes are already represented and applied by PostgreSQL integration tests."
  - "Keep calendar delivery configured to report retryable unavailability until a provider is approved."

requirements-completed: [REQ-rf-g-06, REQ-rf-g-09, REQ-rf-g-12, REQ-rf-g-14, REQ-rf-g-17, REQ-rf-g-18, REQ-rf-g-19, REQ-rf-g-20, REQ-rf-g-21, REQ-rf-g-22, REQ-rf-g-23, REQ-rf-g-25, REQ-rf-g-26, REQ-rf-g-27, REQ-rf-g-28, REQ-rf-g-29, REQ-rf-g-30, REQ-rf-g-41]

# Coverage metadata
coverage:
  - id: D1
    description: "Invited workers complete and replay offline evidence, preserve correction history, and receive verified wage and points through the API lifecycle."
    requirement: REQ-rf-g-18
    verification:
      - kind: e2e
        ref: "src/AgroPlan.IntegrationTests/Api/WorkforceOfflineEndToEndTests.cs#Invited_worker_completes_offline_evidence_lifecycle_and_sees_only_verified_progress"
        status: pass
    human_judgment: false
  - id: D2
    description: "Development Scalar/OpenAPI documents workforce and offline endpoints while Production keeps the documents hidden."
    verification:
      - kind: integration
        ref: "src/AgroPlan.IntegrationTests/Api/WorkforceOpenApiTests.cs#Development_documents_workforce_and_offline_endpoints_in_scalar_contract"
        status: pass
      - kind: integration
        ref: "src/AgroPlan.IntegrationTests/Api/WorkforceOpenApiTests.cs#Production_keeps_workforce_and_offline_docs_hidden"
        status: pass
    human_judgment: false
  - id: D3
    description: "API and Worker resolve the safe provider defaults and phase 3 consumers using existing schema migrations."
    verification:
      - kind: integration
        ref: "dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter FullyQualifiedName~Api"
        status: pass
      - kind: integration
        ref: "dotnet test src/AgroPlan.sln --no-restore"
        status: pass
    human_judgment: false

# Metrics
duration: 17min
completed: 2026-11-10
status: complete
---

# Phase 3 Plan 7: Workforce and Offline Operations Summary

**API and Worker composition now activate phase 3 consumers with safe provider defaults, backed by cross-feature worker lifecycle and Scalar/OpenAPI tests.**

## Performance

- **Duration:** 17 minutes
- **Started:** 2026-11-10T23:10:00Z
- **Completed:** 2026-11-10T23:27:08Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Registered the confirmed-record impact consumer, retryable calendar sync consumer, verified-work rewards consumer, and calendar adapter in the Worker; registered schedule-change notifications and the safe calendar adapter in the API.
- Added an end-to-end API test that accepts an emailed worker invitation, assigns a task, finalizes and replays offline evidence, preserves a correction, verifies wages, awards points, and checks worker privacy.
- Added Scalar/OpenAPI smoke tests for workforce/offline routes in Development and confirmed docs are unavailable in Production.
- Verified the existing feature migrations against PostgreSQL Testcontainers as part of the API tests; no final migration was needed because earlier plans already created the migrations for each schema slice.

## Task Commits

1. **Task 1: Final workforce schema migration and runtime service registration** - `a959bc9` (feat)
2. **Task 2: Cross-feature API lifecycle and Scalar/OpenAPI coverage** - `4c34b84` (test)

## Files Created/Modified

- `src/AgroPlan.Api/Program.cs` - Registers the unconfigured calendar adapter and the task schedule notification service.
- `src/AgroPlan.Worker/Program.cs` - Registers the confirmed-record projection, calendar-sync, and rewards consumers with provider-neutral dependencies.
- `src/AgroPlan.IntegrationTests/IntegrationSupport.cs` - Suppresses the schedule notification background service in isolated API test hosts.
- `src/AgroPlan.IntegrationTests/Api/WorkforceOfflineEndToEndTests.cs` - Covers invite-to-progress lifecycle and offline evidence idempotency.
- `src/AgroPlan.IntegrationTests/Api/WorkforceOpenApiTests.cs` - Covers documented endpoint presence and Production documentation policy.

## Decisions Made

- Reused the existing per-feature migrations from Plans 03-02 to 03-06. Those migrations collectively cover all workforce and offline entities, and integration tests apply them successfully.
- Kept external calendar delivery retryable but unavailable until a provider is approved.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Registered the schedule notification service in API composition and disabled it in isolated test hosts.**
- **Found during:** Task 1 (Final workforce schema migration and runtime service registration)
- **Issue:** Confirmed schedule changes had a notification consumer implementation but the API did not register it, and the test host did not suppress the added polling service.
- **Fix:** Registered the consumer and unconfigured calendar adapter in production composition; excluded the consumer when tests request background-service suppression.
- **Files modified:** `src/AgroPlan.Api/Program.cs`, `src/AgroPlan.IntegrationTests/IntegrationSupport.cs`
- **Verification:** API integration tests and full solution passed.
- **Committed in:** `a959bc9`

### Plan Adjustments

- The plan called for a consolidated `WorkforceOfflineOperations.cs` migration. Plans 03-02 through 03-06 already created migrations for work tasks/evidence, sync receipts/conflicts, rewards, routes, schedule revisions, and delivery status. PostgreSQL Testcontainers applied the migration chain successfully, so no redundant migration was added.
- Task 2's tests passed before any Task 2 source implementation; those behaviors had already been implemented by Plans 03-01 through 03-06. The task therefore remained a test-only integration slice and produced no GREEN implementation commit. `workflow.tdd_mode` is false in this project.

**Total deviations:** 1 auto-fixed issue; 2 plan adjustments.
**Impact on plan:** Runtime wiring was completed, existing migrations were verified without duplication, and the final acceptance test remains focused on cross-feature behavior.

## Issues Encountered

- xUnit/TestHost subprocess communication and Docker named-pipe access require elevated test execution in this environment. The exact test commands were run with the approved environment access.
- Existing xUnit1051 analyzer warnings remain in integration tests outside the files added for this plan; no unrelated test suite warnings were changed.

## User Setup Required

None. Calendar delivery and route generation remain safely unavailable until their respective providers are selected and configured.

## Next Phase Readiness

- Phase 3 implementation plans are complete and ready for independent goal-backward verification.
- Provider selection remains a production gate for live calendar delivery and route generation.

## Self-Check: PASSED

- Summary file exists at the required path.
- Task commits `a959bc9` and `4c34b84` are ancestors of the current `develop` HEAD.
- Measured plan commit count is 2, matching `actuals.commits`.

---
*Phase: 03-workforce-and-offline-operations*
*Completed: 2026-11-10*
