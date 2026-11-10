---
phase: 03-workforce-and-offline-operations
plan: 03
subsystem: api
tags: [dotnet10, xunit-v3, postgres, offline-sync, idempotency, task-conflicts]

# Dependency graph
requires:
  - phase: 03-workforce-and-offline-operations
    provides: Worker assignments, private task evidence staging, and shared evidence finalization service
provides:
  - Farm and worker scoped idempotent operation receipts with stable replay results
  - Per-operation batch outcomes and reconnect evidence finalization
  - Versioned tasks, persisted stale edits, and append-only administrator resolution audit
affects: [offline-client-sync, task-evidence, worker-history, api-documentation]

# Actuals (#2632) — measured from realized diff (chars/4), not a harness counter.
actuals:
  tokens: 21481
  tasks: 2
  commits: 6

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Database transaction advisory lock per farm/worker/operation ID
    - Canonical JSON SHA-256 payload fingerprint and durable replay receipt
    - Task concurrency versions with immutable conflict snapshots and separate resolution entries

key-files:
  created:
    - src/AgroPlan.Api/Controllers/WorkSyncController.cs
    - src/AgroPlan.Domain/Workforce/SyncOperationReceipt.cs
    - src/AgroPlan.Domain/Workforce/TaskSyncConflict.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/WorkSyncOperationReceipts.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/TaskSyncConflicts.cs
    - src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs
  modified:
    - src/AgroPlan.Domain/Workforce/WorkTask.cs
    - src/AgroPlan.Infrastructure/Persistence/TaskEvidenceFinalizationStore.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs

key-decisions:
  - "Fingerprint operation payloads from canonical JSON and scope unique receipts to farm, worker, and client operation ID."
  - "Finalize staged evidence inside the sync transaction so the evidence result and operation receipt commit together."
  - "Keep original client/server conflict snapshots immutable and store administrator resolution as a separate audit row."

patterns-established:
  - "Each batch operation owns its transaction and yields an independent Applied, Replayed, Conflict, Retryable, or Rejected result."
  - "Conflict resolution rechecks the current task version and serializes on the conflict row before appending its single resolution."

requirements-completed: [REQ-rf-g-09, REQ-rf-g-20, REQ-rf-g-23]

coverage:
  - id: D1
    description: "Client operation IDs are fingerprinted, applied once, and replay their stable stored result; each batch item reports separately."
    requirement: REQ-rf-g-09
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkSyncTests.cs#Receipt_requires_a_sha256_fingerprint_and_stable_terminal_result
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs#Sync_applies_an_operation_once_and_returns_the_stored_result_on_replay
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs#Evidence_finalize_is_idempotent_and_a_failed_batch_item_does_not_stop_other_operations
        status: pass
    human_judgment: false
  - id: D2
    description: "A staged task evidence submission can be finalized after reconnect and replayed without another evidence record."
    requirement: REQ-rf-g-20
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs#Evidence_finalize_is_idempotent_and_a_failed_batch_item_does_not_stop_other_operations
        status: pass
    human_judgment: false
  - id: D3
    description: "Stale task edits preserve both versions until an authorized administrator resolves them against the current task version."
    requirement: REQ-rf-g-23
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkSyncTests.cs#Task_sync_conflict_aggregate_is_available
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs#Stale_task_update_preserves_client_and_server_versions_until_admin_resolution
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs#Conflict_resolution_is_rejected_if_the_task_changed_after_conflict_capture
        status: pass
    human_judgment: false

# Metrics
duration: 21min
completed: 2026-11-10
status: complete
plan_head_before: f52f2229b285eb367360ebfe4a110b34f1bf4248
plan_head_after: 782104640807e1775e8711aff25b87c71a2f0a9d
---

# Phase 3 Plan 3: Offline Work Synchronization Summary

**Offline work now synchronizes through durable per-operation receipts, immutable stale-version conflicts, and task-authorized evidence finalization.**

## Performance

- **Duration:** 21 minutes
- **Started:** 2026-11-10T21:27:00Z
- **Completed:** 2026-11-10T21:48:00Z
- **Tasks:** 2 completed
- **Files modified:** 19

## Accomplishments

- Added authenticated farm-scoped `/api/farms/{farmId}/work-sync` processing. Each operation gets an isolated transaction and a persisted response; retries return the recorded result, and reusing an operation ID with a changed payload returns a conflict.
- Added `evidence.finalize` handling that calls the shared task/worker-authorized evidence finalizer and records the receipt in the surrounding database transaction. Failed items do not stop independent batch operations.
- Added task versions, stale client/server snapshots, Owner/Administrator-only resolution with a current-version recheck, and separate immutable resolution audit records.

## Task Commits

1. **Task 1 RED:** `e7e5c9b` — failing offline-sync unit and PostgreSQL/API integration assertions.
2. **Task 1 GREEN:** `60631d8` — operation receipts, per-operation outcomes, and evidence finalization.
3. **Task 2 RED:** `1610bdd` — failing conflict aggregate and stale-version API assertions.
4. **Task 2 GREEN:** `6c6b72e` — versioned tasks, conflict snapshots, and administrative resolution.
5. **Task 2 additional coverage:** `c887530` — conflict replay identity and rejection for a different worker.
6. **Task 2 concurrency fix:** `7821046` — lock conflict records before checking/appending a resolution.

## Files Created/Modified

- `src/AgroPlan.Api/Controllers/WorkSyncController.cs` — batch processing, idempotent replay, evidence finalization, and conflict resolution endpoints.
- `src/AgroPlan.Domain/Workforce/SyncOperationReceipt.cs` — durable payload fingerprint and replay result.
- `src/AgroPlan.Domain/Workforce/TaskSyncConflict.cs` — immutable task version snapshots and resolution decision entity.
- `src/AgroPlan.Domain/Workforce/WorkTask.cs` — version increments for task state, assignment, and schedule changes.
- `src/AgroPlan.Infrastructure/Persistence/TaskEvidenceFinalizationStore.cs` — joins an existing sync transaction when one is supplied.
- `src/AgroPlan.Infrastructure/Persistence/Configurations/` — receipt, conflict, resolution, and task version mappings.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/` — receipt, task version, conflict, and resolution schema.
- `src/AgroPlan.IntegrationTests/Api/WorkSyncTests.cs` — replay, batch continuation, evidence, worker scope, stale conflict, authorization, and resolution coverage.
- `.planning/phases/03-workforce-and-offline-operations/03-03-task{1,2}-*-red-evidence.json` — classifier-accepted TDD RED reports derived from xUnit v3 TRX runs.

## Decisions Made

- SHA-256 fingerprints use canonical JSON and a PostgreSQL advisory lock keyed by farm, worker, and client operation ID, so replay remains safe across API instances.
- Evidence finalization joins the receipt transaction, keeping finalized evidence metadata and operation outcome committed together.
- Conflicts preserve both task snapshots; resolution uses a separate row so the original conflict is not rewritten.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Added the task version required by the sync conflict plan.**
- **Found during:** Task 2, stale version implementation
- **Issue:** Plan 03-02 did not leave a task concurrency version available to compare against a client's base version.
- **Fix:** Added a persisted task version, concurrency-token mapping, and migration; lifecycle changes increment it.
- **Files modified:** `WorkTask.cs`, `WorkTaskConfiguration.cs`, `TaskSyncConflicts.cs`
- **Verification:** Unit tests confirmed version increments; PostgreSQL tests proved stale values remain unchanged until resolution.
- **Committed in:** `6c6b72e`

**2. [Rule 2 - Concurrency] Serialize conflict resolution.**
- **Found during:** Task 2, API review
- **Issue:** Two administrators resolving a conflict with the server version could both observe no resolution before either inserted its audit row.
- **Fix:** Lock the conflict row before checking for an existing resolution and append exactly one decision.
- **Files modified:** `WorkSyncController.cs`
- **Verification:** PostgreSQL conflict suite passed after the lock was added.
- **Committed in:** `7821046`

**Total deviations:** 2 auto-fixed issues. Both protect the planned version and audit guarantees; no dependencies or deferred scope were added.

## Issues Encountered

- Docker named-pipe access and the xUnit testhost required elevated execution for PostgreSQL/Testcontainers commands. The engine was reachable and all planned integration tests ran.
- MSBuild project-reference child nodes required `-m:1 -nr:false`; with those flags, .NET 10.0.404 compiled and ran the tests normally.
- Integration test compilation reports xUnit1051 cancellation-token analyzer warnings in existing suites and the new integration test file. They do not fail the tests; no broad warning cleanup was included in this plan.

## TDD Gate Compliance

- **Task 1 RED:** `Sync_operation_receipt_and_conflict_aggregates_are_available` failed its intended missing-aggregate assertion; `Sync_applies_an_operation_once_and_returns_the_stored_result_on_replay` failed because the sync route returned 404. Both reports passed `check tdd-red-evidence`.
- **Task 1 GREEN:** 2 focused unit tests and 2 PostgreSQL integration tests passed, including evidence replay and independent batch failure.
- **Task 2 RED:** `Task_sync_conflict_aggregate_is_available` failed its intended missing-type assertion; `Stale_task_update_preserves_client_and_server_versions_until_admin_resolution` failed because `task.update` was unsupported. Both reports passed `check tdd-red-evidence`.
- **Task 2 GREEN:** 8 focused unit tests (including task version tests) and 4 PostgreSQL integration tests passed, including denied Worker resolution, conflict replay, immutable snapshots, and stale resolution rejection.
- **REFACTOR:** No separate refactor commit was needed; concurrency hardening is recorded as Rule 2 above.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~WorkSyncTests|FullyQualifiedName~WorkTaskTests" -m:1 -nr:false` — 8 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~WorkSyncTests" -m:1 -nr:false` — 4 passed with PostgreSQL Testcontainers.
- `dotnet build src/AgroPlan.Api/AgroPlan.Api.csproj --no-restore -m:1 -nr:false` — passed, 0 warnings, 0 errors.
- `dotnet test src/AgroPlan.sln -m:1 -nr:false` — full suite passed: 108 unit and 39 PostgreSQL/RabbitMQ integration tests.

## Next Phase Readiness

Plan 03-04 through 03-07 can build on the synchronized task version and receipt tables. The API exposes result states needed by the offline client; downstream plans still own additional task, reward, routing, and calendar workflows.

## Self-Check: PASSED

- Summary output path exists and all created implementation artifacts listed above are tracked.
- Plan commits `e7e5c9b`, `60631d8`, `1610bdd`, `6c6b72e`, `c887530`, and `7821046` are ancestors of `develop`.
- The plan evaluation scope resolves to all six plan commits; no generated test-result files remain staged or untracked.
