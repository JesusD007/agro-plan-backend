---
phase: 03-workforce-and-offline-operations
plan: 06
subsystem: workforce-scheduling
tags: [dotnet10, xunit-v3, postgres, rabbitmq, outbox, calendar-sync, signalr]

requires:
  - phase: 03-workforce-and-offline-operations
    provides: Farm-scoped work tasks, immutable field records, PostgreSQL outbox, and verified worker membership
provides:
  - Confirmed field-record events and deterministic, source-backed task schedule proposals
  - Append-only task schedule revisions with administrator confirmation, worker notification, and audit history
  - Provider-neutral external calendar adapter with per-event retry and terminal delivery state
affects: [workforce, task-scheduling, field-records, calendar-integration, phase-03-plan-07]

actuals:
  tokens: 29987
  tasks: 3
  commits: 6
  commits_measured: 6
  plan_head_before: 8b7dac1173e529db98e10d4c166fc636e8ecd523
  plan_head_after: cfcb9aa2ecdcef3fe109df229aa355590ba57367

tech-stack:
  added: []
  patterns:
    - Confirmed record writes and farm-record outbox events are persisted in one PostgreSQL SaveChanges transaction
    - Deterministic matching requires an exact farm, parcel, crop-cycle, operation-kind, and task-status match
    - Task date changes append revisions; only a current farm owner or administrator can apply a proposal
    - Calendar operations use event ids as idempotency keys and persist retry/backoff outcomes independently

key-files:
  created:
    - src/AgroPlan.Application/Abstractions/IExternalWorkCalendar.cs
    - src/AgroPlan.Application/Workforce/WorkTaskImpactProjector.cs
    - src/AgroPlan.Api/Controllers/TaskScheduleController.cs
    - src/AgroPlan.Domain/Workforce/TaskScheduleRevision.cs
    - src/AgroPlan.Domain/Workforce/CalendarDeliveryStatus.cs
    - src/AgroPlan.Worker/Jobs/ConfirmedRecordTaskImpactConsumer.cs
    - src/AgroPlan.Worker/Jobs/TaskCalendarSyncConsumer.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/TaskScheduleRevisions.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/CalendarDeliveryStatuses.cs
    - src/AgroPlan.UnitTests/Workforce/TaskScheduleTests.cs
    - src/AgroPlan.IntegrationTests/Worker/ConfirmedRecordTaskImpactTests.cs
    - src/AgroPlan.IntegrationTests/Worker/TaskCalendarSyncTests.cs
    - src/AgroPlan.IntegrationTests/Api/TaskScheduleControllerTests.cs
    - .planning/phases/03-workforce-and-offline-operations/03-06-task1-red-evidence.json
    - .planning/phases/03-workforce-and-offline-operations/03-06-task2-red-evidence.json
    - .planning/phases/03-workforce-and-offline-operations/03-06-task3-red-evidence.json
  modified:
    - src/AgroPlan.Api/Controllers/LogbookController.cs
    - src/AgroPlan.Api/Controllers/IrrigationEntriesController.cs
    - src/AgroPlan.Api/Controllers/InputApplicationsController.cs
    - src/AgroPlan.Api/Controllers/HarvestRecordsController.cs
    - src/AgroPlan.Api/Hubs/JobStatusHub.cs
    - src/AgroPlan.Domain/Workforce/WorkTask.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs

key-decisions:
  - "No external calendar provider or credentials are selected; the default adapter reports an explicit retryable unavailable state."
  - "A confirmed field record proposes a task date only when exactly one pending or in-progress task matches all explicit farm, parcel, crop-cycle, and operation links."
  - "Task proposals never change a task before an active farm owner or administrator confirms against the captured task version."
  - "Calendar delivery status is per outbox event; retries are idempotent and do not roll back the saved schedule."

patterns-established:
  - "Source-backed task schedule proposals retain the source outbox id and immutable source record id/version."
  - "Worker-facing schedule notifications are delivered to the assigned worker's farm/user SignalR group after confirmation."
  - "Calendar provider calls happen after durable event/status persistence, with bounded exponential retry and per-operation isolation."

requirements-completed: [REQ-rf-g-41]

coverage:
  - id: D1
    description: "Confirmed field records emit immutable events, and one exact task match creates an explained proposal without changing the task; ambiguous matches are ignored."
    requirement: REQ-rf-g-41
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/TaskScheduleTests.cs#Confirmed_record_produces_explained_proposal_for_only_exact_pending_match
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/ConfirmedRecordTaskImpactTests.cs#Consumer_creates_one_unapplied_source_backed_proposal_and_ignores_ambiguous_replays
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/ConfirmedRecordTaskImpactTests.cs#Confirmed_record_and_correction_commit_with_immutable_outbox_events
        status: pass
    human_judgment: false
  - id: D2
    description: "Farm administrators append and confirm schedule revisions after version checks; worker notification and external calendar events commit with the task update."
    requirement: REQ-rf-g-41
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/TaskScheduleTests.cs#Schedule_revision_requires_admin_confirmation_and_rechecks_task_version
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/TaskScheduleControllerTests.cs#Administrator_reschedule_appends_revision_and_calendar_outbox_event
        status: pass
    human_judgment: false
  - id: D3
    description: "Calendar sync reports retryable or terminal outcomes per event, exposes status to the assigned worker/farm administrator, and continues independent operations."
    requirement: REQ-rf-g-41
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/TaskScheduleTests.cs#Delivery_status_persists_backoff_and_makes_success_terminal
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/TaskCalendarSyncTests.cs#Delivery_failures_are_retryable_per_operation_and_never_revert_saved_tasks
        status: pass
    human_judgment: false

duration: 30min
completed: 2026-11-10
status: complete
---

# Phase 3 Plan 6: Source-Backed Task Scheduling and Calendar Delivery Summary

**Confirmed farm operations now create explainable task-date proposals, and approved schedule changes keep an immutable history while external calendar delivery retries independently.**

## Performance

- **Duration:** 30 minutes
- **Started:** 2026-11-10T22:37:00Z
- **Completed:** 2026-11-10T23:07:28Z
- **Tasks:** 3 completed
- **Files modified:** 32

## Accomplishments

- Added transactional `farm-record.confirmed` outbox events for logbook, irrigation, input-application, and harvest records; corrections append a new source record/version and event.
- Added deterministic source-backed task impact projection. Only an exact farm, parcel, crop-cycle, operation-kind, and pending/in-progress task match can create a proposal; unmatched or ambiguous operations do not mutate task dates.
- Added task schedule revision history, current-membership owner/administrator confirmation, source/task version rechecks, worker SignalR notifications, and atomic calendar/notification outbox events.
- Added the provider-neutral `IExternalWorkCalendar` port and explicitly unconfigured adapter. Each calendar event gets its own persistent status, idempotency key, bounded retry/backoff, terminal outcome, and farm-scoped API visibility.
- Added xUnit v3 domain tests and PostgreSQL/Testcontainers API and Worker tests for projection, immutability, authorization, retry, idempotency, and independent operations.

## Task Commits

1. **Task 1 RED:** `f8930ff` — failing schedule revision capability test.
2. **Task 1 GREEN:** `baca640` — source-backed task proposals and confirmed-record outbox consumer.
3. **Task 2 RED:** `d2bd1c6` — failing administrator reschedule integration test.
4. **Task 2 GREEN:** `108fd44` — append-only administrator revisions and durable notifications.
5. **Task 3 RED:** `b07888f` — failing provider-neutral calendar contract test.
6. **Task 3 GREEN:** `cfcb9aa` — per-event calendar status, retry loop, and safe unconfigured adapter.

**Plan metadata:** pending close-out commit.

## Decisions Made

- Calendar sync remains disabled against external providers until one is selected and configured; no provider SDK or credentials were added.
- A source event is ignored for task projection unless every linking field is explicit and exactly one eligible task matches.
- Direct administrator changes are immediately confirmed through the same append-only revision path; source-backed proposals require a separate version-checked confirmation.
- Calendar delivery is a side effect. The PostgreSQL schedule and its outbox records remain authoritative when a provider call fails.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Added schedule revision schema migration in Plan 03-06.**
- **Found during:** Task 2 integration verification.
- **Issue:** The PostgreSQL API integration test could not verify persisted history without a database table, although the original plan text assigned final migration work to Plan 03-07.
- **Fix:** Added separate migrations for task schedule revisions and per-event calendar delivery status so the current plan's API and Worker tests exercise real PostgreSQL persistence. Plan 03-07 still owns final DI registration and any remaining schema changes.
- **Files modified:** `src/AgroPlan.Infrastructure/Persistence/Migrations/TaskScheduleRevisions.cs`, `src/AgroPlan.Infrastructure/Persistence/Migrations/CalendarDeliveryStatuses.cs`
- **Verification:** Task schedule and calendar Testcontainers integration tests passed.
- **Committed in:** `108fd44`, `cfcb9aa`.

**2. [Rule 2 - Missing critical functionality] Hosted the worker notification dispatcher in the API composition boundary.**
- **Found during:** Task 2 implementation.
- **Issue:** The worker project has no dependency on the API's SignalR hub and should not take one to send UI notifications.
- **Fix:** Added an API hosted service that delivers confirmed revision updates to the assigned worker's authenticated farm/user SignalR group and persists `NotifiedAt` for retry safety. The hub now permits an active Worker to join only their own farm/user group.
- **Files modified:** `src/AgroPlan.Api/Services/TaskScheduleNotificationConsumer.cs`, `src/AgroPlan.Api/Hubs/JobStatusHub.cs`
- **Verification:** Full solution build and integration tests passed; Plan 03-07 must register the hosted service.
- **Committed in:** `108fd44`.

**Total deviations:** 2 auto-fixed (1 Rule 3, 1 Rule 2).
**Impact on plan:** PostgreSQL-backed behaviors are testable now and responsibilities remain separated. Plan 03-07 must finish API/Worker DI registration for the new consumers/services.

## Issues Encountered

- Initial NuGet restore was blocked by sandbox network restrictions, and a relative `DOTNET_CLI_HOME` caused MSBuild child processes to look in project-local locations. An approved elevated restore with absolute `DOTNET_CLI_HOME` and `NUGET_PACKAGES` paths fixed it; no package references were added.
- Existing xUnit analyzer warnings (`xUnit1051`) remain in unrelated integration test files; the new tests use `TestContext.Current.CancellationToken`.

## TDD Gate Compliance

- Task 1 RED `Confirmed_record_projection_and_append_only_schedule_revision_are_available` executed and failed its planned assertion; `03-06-task1-red-evidence.json` returned `RED_EVIDENCE_OK`.
- Task 2 RED `Administrator_reschedule_appends_revision_and_calendar_outbox_event` reached the authenticated API and received 404 for the not-yet-implemented endpoint; `03-06-task2-red-evidence.json` returned `RED_EVIDENCE_OK`.
- Task 3 RED `Calendar_adapter_contract_and_per_event_delivery_status_are_available` executed and failed because the adapter contract was absent; `03-06-task3-red-evidence.json` returned `RED_EVIDENCE_OK`.
- GREEN verification passed 7 focused unit tests and 4 focused PostgreSQL integration tests.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "FullyQualifiedName~TaskScheduleTests" -m:1 -nr:false` — 7 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~TaskScheduleControllerTests|FullyQualifiedName~ConfirmedRecordTaskImpactTests|FullyQualifiedName~TaskCalendarSyncTests" -m:1 -nr:false` — 4 passed with PostgreSQL Testcontainers.
- `dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false` — full solution passed: 126 unit tests and 49 PostgreSQL/RabbitMQ integration tests (175 total).

## User Setup Required

No provider credentials are required while calendar integration is disabled. Select and configure an external calendar provider before enabling real synchronization.

## Next Phase Readiness

Plan 03-07 must register `ConfirmedRecordTaskImpactConsumer`, `TaskCalendarSyncConsumer`, `UnconfiguredWorkCalendar`, and `TaskScheduleNotificationConsumer` in their host compositions and verify the migrations on the complete solution. The user-selected calendar provider remains intentionally unconfigured.

## Self-Check: PASSED

- All source, API, Worker, persistence, and test files referenced above exist.
- All six task commits are ancestors of `develop`; plan metadata will be committed after roadmap/state updates.
- The full solution test run passed 126 unit and 49 PostgreSQL/RabbitMQ integration tests.
