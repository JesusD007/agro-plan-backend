---
phase: 03-workforce-and-offline-operations
plan: 02
subsystem: workforce, api, database
tags: [aspnet-core, xunit-v3, postgres, testcontainers, task-evidence, wage-history]

requires:
  - phase: 03-workforce-and-offline-operations
    provides: Farm-scoped Worker memberships and invitation acceptance from Plan 03-01
provides:
  - Farm-scoped task assignment, worker-only task visibility, and status lifecycle
  - Private, task-bound evidence uploads with consent-controlled location/time and linked revisions
  - Administrator review decisions, verified wage history, and a work-task-verified outbox event
affects: [03-03-offline-sync, 03-04-rewards, 03-06-scheduling, workforce, api, database]

tech-stack:
  added: []
  patterns:
    - Active farm membership and task assignment are re-read from PostgreSQL for worker access
    - Evidence finalization is shared through an Application service and persistence-store abstraction
    - Correction evidence is appended as a new revision linked to the preserved prior revision
    - Wage history is created atomically only when an administrator verifies submitted work

key-files:
  created:
    - src/AgroPlan.Domain/Workforce/WorkTask.cs
    - src/AgroPlan.Domain/Workforce/TaskEvidenceSubmission.cs
    - src/AgroPlan.Domain/Workforce/TaskReviewDecision.cs
    - src/AgroPlan.Domain/Workforce/VerifiedWageEntry.cs
    - src/AgroPlan.Api/Controllers/WorkTasksController.cs
    - src/AgroPlan.Api/Controllers/TaskEvidenceController.cs
    - src/AgroPlan.Api/Controllers/TaskReviewsController.cs
    - src/AgroPlan.Application/Workforce/TaskEvidenceFinalizationService.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/WorkTasksAndEvidence.cs
    - src/AgroPlan.UnitTests/Workforce/WorkTaskTests.cs
    - src/AgroPlan.UnitTests/Workforce/TaskEvidenceTests.cs
    - src/AgroPlan.IntegrationTests/Api/WorkTaskAndEvidenceTests.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.IntegrationTests/IntegrationSupport.cs
    - src/AgroPlan.Application/Abstractions/IFileStorage.cs
    - src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs

key-decisions:
  - Worker and administrator authorization checks use current PostgreSQL membership on each request; workers only see assigned tasks and their history.
  - Uploads are private, bound to farm/task/worker/revision/client operation, limited to JPEG, PNG, or WebP up to 10 MiB, and idempotent on the same client operation id.
  - Location and captured time are stored only with explicit worker consent; correction evidence is a new linked revision and the earlier evidence remains available.
  - Only owner/administrator approval confirms the wage set at assignment and writes one wage-history row; no payment is initiated.

requirements-completed: [REQ-rf-g-18, REQ-rf-g-20, REQ-rf-g-21, REQ-rf-g-22]

coverage:
  - id: D1
    description: Farm workers see only their assigned tasks and can advance valid task states.
    requirement: REQ-rf-g-18
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkTaskTests.cs#Work_task_aggregate_is_available_for_assignment
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkTaskAndEvidenceTests.cs#Assigned_worker_only_sees_own_task_and_verified_task_confirms_wage_without_payment
        status: pass
    human_judgment: false
  - id: D2
    description: Required photos, content limits, and consent-controlled metadata are enforced for task evidence.
    requirement: REQ-rf-g-20
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/TaskEvidenceTests.cs#Task_evidence_submission_aggregate_is_available
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkTaskAndEvidenceTests.cs#Required_photo_blocks_submission_and_invalid_uploads_are_rejected
        status: pass
    human_judgment: false
  - id: D3
    description: Evidence finalization is task-bound and idempotent; correction review preserves the prior revision and requires a newer revision when prior evidence was reviewed.
    requirement: REQ-rf-g-21
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkTaskAndEvidenceTests.cs#Assigned_worker_only_sees_own_task_and_verified_task_confirms_wage_without_payment
        status: pass
    human_judgment: false
  - id: D4
    description: Administrator decisions are auditable and only approved tasks create a single verified wage-history entry.
    requirement: REQ-rf-g-22
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkTaskTests.cs#Review_decision_requires_reason_and_verified_wage_is_an_immutable_fact
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkTaskAndEvidenceTests.cs#Rejected_task_does_not_create_a_verified_wage_entry
        status: pass
    human_judgment: false

actuals:
  tokens: 23006
  tasks: 3
  commits: 8
  plan_head_before: 59698866c25899d2253daf8c3b9d6b9934c98a66
  plan_head_after: d2a6193c197dd574e9b3a914304ee714675a669e

duration: 23min
completed: 2026-11-10
status: complete
---

# Phase 3 Plan 2: Worker Tasks, Evidence, and Wage Review Summary

**Farm-scoped task assignments use private consent-aware evidence revisions and create immutable wage-history records only after administrator approval.**

## Performance

- **Duration:** 23 minutes
- **Started:** 2026-11-10T17:01:18-04:00
- **Completed:** 2026-11-10T17:23:52-04:00
- **Tasks:** 3/3
- **Files modified:** 28

## Accomplishments

- Added farm task assignment, reassignment before work starts, worker-only task list/detail views, and task lifecycle transitions.
- Added private signed uploads bound to the farm, task, active worker, revision, and client operation id; same-operation retries return the same upload authorization.
- Added task-specific photo enforcement and consent-gated location/time fields, with verified object metadata before finalization.
- Added immutable review decisions and linked evidence revisions; correction resubmission requires evidence newer than the reviewed revision when one existed.
- Added one verified wage-history row and a `work-task.verified` outbox event on approval; rejected and correction-requested tasks do not create wage rows.

## Task Commits

1. **Task 1 RED:** `7436ce2` — failing work-task aggregate test.
2. **Task 1 GREEN:** `03e4825`, `5e27f6c` — work-task lifecycle, farm authorization, endpoints, and persistence configuration.
3. **Task 2 RED:** `d8bd9ed` — failing evidence-submission test.
4. **Task 2 GREEN:** `1ee74cb` — consent-aware private upload, finalization, and linked evidence revisions.
5. **Task 3 RED:** `309c0a3` — failing review and wage-record test.
6. **Task 3 GREEN:** `b2687c5` — review decisions, verified wage history, outbox event, migration, and PostgreSQL integration coverage.
7. **Correction revision guard:** `d2a6193` — require new evidence after correction review when the reviewed submission contained evidence.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~WorkTaskTests|FullyQualifiedName~TaskEvidenceTests"` — 6 passed, 0 failed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~WorkTaskAndEvidenceTests"` — 3 passed, 0 failed, using PostgreSQL Testcontainers with Docker Desktop.
- `dotnet build src/AgroPlan.Api/AgroPlan.Api.csproj --no-restore -m:1 -nr:false` — passed.
- TDD RED evidence for all three tasks is stored in the phase directory; xUnit v3 TRX identities and outcomes were mapped faithfully into the GSD-supported TAP evidence format and accepted by `check tdd-red-evidence`.

## Decisions Made

- Active farm membership and assigned-worker scope are checked against PostgreSQL for each task, evidence, and history request.
- Correction decisions retain the prior submission and require a higher evidence revision before resubmission when evidence was reviewed.
- Wages are recorded in the original amount/unit from assignment only after approval; the API does not execute payments.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Authorization correctness] Revalidated worker membership and assignment during evidence finalization.**
- **Found during:** Task 2
- **Issue:** The upload path had to remain bound to a current active Worker membership even after the initial route authorization check.
- **Fix:** Rechecked task assignment and active Worker membership inside the persistence finalization transaction.
- **Verification:** PostgreSQL integration tests reject finalization by another worker.
- **Files modified:** `src/AgroPlan.Infrastructure/Persistence/TaskEvidenceFinalizationStore.cs`

**2. [Rule 2 - Evidence integrity] Require a new evidence revision after a correction decision.**
- **Found during:** Task 2 integration verification
- **Issue:** A prior finalized photo could otherwise satisfy a required-photo check after an administrator requested a correction.
- **Fix:** Compare the latest finalized revision with the reviewed submission and reject resubmission until a newer revision is finalized.
- **Verification:** PostgreSQL integration test confirms immediate resubmission fails, then succeeds with a linked revision.
- **Files modified:** `src/AgroPlan.Api/Controllers/TaskEvidenceController.cs`, `src/AgroPlan.IntegrationTests/Api/WorkTaskAndEvidenceTests.cs`
- **Commit:** `d2a6193`

### Environment Notes

- Docker named-pipe access and the xUnit testhost required elevated test execution in this workspace.
- NuGet vulnerability metadata was unavailable from `api.nuget.org` during restore (`NU1900`); cached packages were sufficient to build and run the target suites.
- MSBuild required `-m:1 -nr:false` to avoid sandbox child-node failures that otherwise reported no compiler errors.

**Total deviations:** 2 correctness fixes. No dependencies were added.

## Issues Encountered

- Integration tests first exposed an incorrect migration ordering before `CropCycles`; the task migration was assigned the next migration id after existing schema migrations and the PostgreSQL suite then passed.

## Next Phase Readiness

- Plan 03-03 can reuse `TaskEvidenceFinalizationService` and the farm/task-bound operation model for offline evidence synchronization.
- Requirements declared by multiple Phase 3 plans remain pending until the final plan that declares each requirement is completed.

## TDD Gate Compliance

- **RED:** `Work_task_aggregate_is_available_for_assignment`, `Task_evidence_submission_aggregate_is_available`, and `Review_decision_and_verified_wage_are_separate_auditable_records` each executed and failed the intended missing-capability assertion; all three records passed `check tdd-red-evidence`.
- **GREEN:** The corresponding unit tests and PostgreSQL/API integration suite passed after implementation.
- **REFACTOR:** Task correction verification identified and fixed stale evidence reuse; the 3-test integration suite passed afterward.

## Self-Check: PASSED

- All domain, API, persistence, migration, unit-test, and integration-test artifacts exist.
- Task commits `7436ce2`, `03e4825`, `309c0a3`, `d8bd9ed`, `5e27f6c`, `1ee74cb`, `b2687c5`, and `d2a6193` are ancestors of `HEAD`.
- The plan ledger measures 8 commits from `59698866c25899d2253daf8c3b9d6b9934c98a66` through `d2a6193c197dd574e9b3a914304ee714675a669e`.
