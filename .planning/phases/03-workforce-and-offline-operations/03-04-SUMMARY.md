---
phase: 03-workforce-and-offline-operations
plan: 04
subsystem: workforce
tags: [dotnet10, xunit-v3, postgres, rabbitmq, worker-progress, rewards]

requires:
  - phase: 03-workforce-and-offline-operations
    provides: Verified task events and farm-scoped worker assignments
provides:
  - Idempotent verified-work points and threshold badges from persisted outbox events
  - Farm worker settings for ranking visibility, timezone, badges, and team goals
  - Private progress/history APIs and audited manual reward APIs
affects: [workforce, gamification, worker-privacy, reports]

actuals:
  tokens: 18720
  tasks: 2
  commits: 4
  commits_measured: 4
  plan_head_before: db475676da6d4f87a426525aef44eca8fd9eb96e
  plan_head_after: 3d0335c0cd11413526080e498b47f52e28943007

tech-stack:
  added: []
  patterns:
    - Persisted outbox identity and verified task state are rechecked before processing rewards
    - Farm and worker row locks plus unique ledger indexes make duplicate broker deliveries idempotent
    - Rankings default off; private progress is scoped to the worker or current farm owner/administrator

key-files:
  created:
    - src/AgroPlan.Domain/Workforce/FarmWorkerSettings.cs
    - src/AgroPlan.Domain/Workforce/VerifiedWorkReward.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/WorkerProgress.cs
    - src/AgroPlan.Worker/Jobs/VerifiedWorkRewardsConsumer.cs
    - src/AgroPlan.Api/Controllers/WorkerProgressController.cs
    - src/AgroPlan.Api/Controllers/WorkerRewardsController.cs
    - src/AgroPlan.UnitTests/Workforce/WorkerProgressTests.cs
    - src/AgroPlan.IntegrationTests/Api/WorkerProgressTests.cs
  modified:
    - src/AgroPlan.Api/Controllers/WorkTasksController.cs
    - src/AgroPlan.Domain/Workforce/WorkTask.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/WorkTaskConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmWorkerSettingsConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/VerifiedWorkRewardConfiguration.cs
    - src/AgroPlan.Worker/Program.cs

key-decisions:
  - "Award points only after a persisted, matching verified-task outbox event is processed; deduplicate by farm, task, and award rule."
  - "Objective task points may include a bonus for finalized evidence presence; no image-quality or speed scoring is used."
  - "Use explicit farm timezone for streaks and return unavailable when no timezone is configured."
  - "Keep rankings disabled by default and record manual rewards as auditable recognition with no payment event."

requirements-completed: [REQ-rf-g-25, REQ-rf-g-26, REQ-rf-g-27, REQ-rf-g-28, REQ-rf-g-29, REQ-rf-g-30]

coverage:
  - id: D1
    description: "Verified task events create objective point and deterministic badge ledger entries once; invalid/unverified event payloads are rejected."
    requirement: REQ-rf-g-25
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkerProgressTests.cs#Task_carries_objective_award_values_and_ledger_records_rule_version
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerProgressTests.cs#Only_persisted_verified_event_awards_once_with_objective_evidence_bonus
        status: pass
    human_judgment: false
  - id: D2
    description: "Farm timezone governs streak dates, and streak status is explicitly unavailable until configured."
    requirement: REQ-rf-g-26
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkerProgressTests.cs#Rankings_default_off_and_streak_requires_an_explicit_farm_timezone
        status: pass
    human_judgment: false
  - id: D3
    description: "Personal histories are visible to the worker and farm owner/administrator; rankings are admin-controlled and off by default."
    requirement: REQ-rf-g-27
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerProgressTests.cs#Progress_history_is_private_and_rankings_are_disabled_by_default
        status: pass
    human_judgment: false
  - id: D4
    description: "Manual reward entries record the granting administrator and description without publishing payment events."
    requirement: REQ-rf-g-30
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkerProgressTests.cs#Manual_reward_requires_auditable_administrator_and_description
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerProgressTests.cs#Manual_reward_is_audited_and_does_not_publish_payment_event
        status: pass
    human_judgment: false

duration: 22min
completed: 2026-11-10
status: complete
---

# Phase 3 Plan 4: Verified Work Progress Summary

**Verified tasks now drive an idempotent points ledger and private progress APIs, while administrators control optional rankings and append manual non-payment rewards.**

## Performance

- **Duration:** 22 minutes
- **Started:** 2026-11-10T21:50:00Z
- **Completed:** 2026-11-10T22:12:00Z
- **Tasks:** 2 completed
- **Files modified:** 18

## Accomplishments

- Added per-task objective point values, evidence-presence bonuses, farm timezone, deterministic badge threshold, and team goal target.
- Added RabbitMQ routing and a verified-work consumer that validates the matching persisted outbox event and current verified task, then writes points/badge ledger rows once under database locks.
- Added farm-scoped progress, history, ranking settings/board, and manual reward endpoints with live membership/worker authorization. Manual rewards do not create outbox or payment events.
- Added the PostgreSQL migration, xUnit v3 unit tests, and API/PostgreSQL integration tests.

## Task Commits

1. **Task 1 RED:** `42c1496` — missing worker progress settings and ledger assertion.
2. **Task 1 GREEN:** `ade40bc` — verified-work consumer, scoring, configuration, and schema.
3. **Task 2 RED:** `442d8b9` — progress privacy and manual reward API assertions.
4. **Task 2 GREEN:** `3d0335c` — private progress, ranking controls, and manual reward APIs.

## Key Files

- `FarmWorkerSettings.cs` — timezone, rankings, badge threshold, and team goal configuration.
- `VerifiedWorkReward.cs` — append-only point, badge, and manual-reward ledger row.
- `VerifiedWorkRewardsConsumer.cs` — validates persisted verified events and creates idempotent awards.
- `WorkerProgressController.cs` — worker/admin progress, history, board, and settings endpoints.
- `WorkerRewardsController.cs` — private reward history and auditable manual grants.
- `WorkerProgress.cs` — PostgreSQL schema and task scoring columns.
- `WorkerProgressTests.cs` — domain, API privacy, idempotency, and payment-side-effect coverage.

## Decisions Made

- Use finalized evidence presence as an optional objective task bonus; the service never evaluates image clarity or speed.
- Reject verified-work messages that do not match both a persisted outbox event and the task's current verified state.
- Omit streak values until a farm timezone is configured rather than falling back to server-local dates.
- Keep leaderboard visibility off until an owner/administrator enables it for that farm.
- Store administrator rewards in the audit ledger without labeling them paid or emitting payment events.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Added task-level objective award values.**
- **Found during:** Task 1 (verified-work points)
- **Issue:** `WorkTask` had no way to store per-task objective point criteria, so the plan's configured criteria could not be honored.
- **Fix:** Added bounded points-for-verification and finalized-evidence bonus fields to task creation, persistence, and API response.
- **Files modified:** `WorkTask.cs`, `WorkTasksController.cs`, `WorkTaskConfiguration.cs`, `WorkerProgress.cs`
- **Verification:** xUnit v3 tests and API/PostgreSQL integration tests passed.
- **Commit:** `ade40bc`

## TDD Gate Compliance

- **Task 1 RED:** `Farm_settings_and_verified_work_ledger_are_available` executed and failed because `FarmWorkerSettings` was missing; machine evidence passed `RED_EVIDENCE_OK` and semantic inspection confirmed an assertion failure.
- **Task 1 GREEN:** 3 focused unit tests passed. The integration test proved that only a persisted event for a verified task awards points, finalized evidence adds only the configured objective bonus, duplicate delivery adds nothing, and an unverified task is rejected.
- **Task 2 RED:** The API integration assertions executed against PostgreSQL and received HTTP 404 because progress/reward controllers were absent; machine evidence passed `RED_EVIDENCE_OK`.
- **Task 2 GREEN:** 3 focused API/PostgreSQL integration tests passed for personal scope, board default/activation, administrator-only settings, manual reward audit, and absence of payment events.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~WorkerProgressTests" -m:1 -nr:false` — 3 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~WorkerProgressTests" -m:1 -nr:false` — 3 passed with PostgreSQL Testcontainers.
- `dotnet test src/AgroPlan.sln -m:1 -nr:false --no-restore` — full solution passed: 111 unit tests and 42 PostgreSQL/RabbitMQ integration tests (153 total).
- API and Worker build successfully as part of the solution test run.

## Self-Check: PASSED

- Summary, domain, persistence, Worker, API, and test artifacts exist at their recorded paths.
- All four plan task commits are ancestors of `develop`.
- Plan commit count and realized diff size were measured from `plan_head_before`.
