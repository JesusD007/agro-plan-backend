---
phase: 02-farm-operations-and-safety
fixed_at: 2026-11-10T17:35:00Z
review_path: .planning/phases/02-farm-operations-and-safety/02-REVIEW.md
iteration: 2
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 02: Code Review Fix Report

**Fixed at:** 2026-11-10
**Source review:** `.planning/phases/02-farm-operations-and-safety/02-REVIEW.md`
**Iteration:** 2

**Summary:**
- Findings in scope: 6
- Fixed: 6
- Skipped: 0

## Fixed Issues

### WR-01: One operation can complete every calendar task of the same kind

**Files modified:** `src/AgroPlan.Application/Calendars/CropCalendarEvaluator.cs`, `src/AgroPlan.UnitTests/Calendars/CropCalendarEvaluatorTests.cs`
**Commit:** `95289a8`
**Applied fix:** Calendar evaluation consumes each matching progress event once, so one irrigation operation cannot complete multiple same-kind tasks. Added a regression with two tasks and one operation.
**Status:** fixed: requires human verification
**Verification:** Main checkout; `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1 --filter FullyQualifiedName~CropCalendarEvaluatorTests` — 5 passed.

### WR-02: Date-filtered CSVs can mark a corrected expense as active

**Files modified:** `src/AgroPlan.Api/Controllers/OperationsReportsController.cs`, `src/AgroPlan.Api/Controllers/AdvisorReportsController.cs`, `src/AgroPlan.IntegrationTests/Api/OperationsReportsTests.cs`
**Commit:** `d7a6514`
**Applied fix:** Both CSV endpoints resolve correction links across all farm- and cycle-scoped expense rows before applying date filters. Added an integration case proving owner and advisor CSVs mark an expense inactive when its correction falls outside the selected period.
**Status:** fixed: requires human verification
**Verification:** Main checkout; focused PostgreSQL API integration test — 1 passed.

### WR-03: A corrected planting record makes new schedule proposals unconfirmable

**Files modified:** `src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs`, `src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs`
**Commit:** `cd26236`
**Applied fix:** Proposal creation filters superseded sowing records using linked correction IDs before selecting planting evidence. Updated the end-to-end test to create a sowing correction and verify the proposal cites the active record and can be confirmed.
**Status:** fixed: requires human verification
**Verification:** Main checkout; focused PostgreSQL/RabbitMQ schedule proposal integration test — 1 passed.

### WR-04: Worker redelivery cannot recover proposals left in Processing

**Files modified:** `src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs`, `src/AgroPlan.Infrastructure/Persistence/Configurations/CropCalendarConfiguration.cs`, `src/AgroPlan.Infrastructure/Persistence/Migrations/ScheduleProposalProcessingLease.cs`, `src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs`, `src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs`
**Commit:** `93babf4`
**Applied fix:** Added processing lease timestamps and bounded attempt counts. Expired claims are atomically reclaimed; exhausted claims transition to `Failed`; deliveries encountering an active claim are requeued after a short delay. The integration case simulates an interrupted, expired claim and confirms redelivery reaches `Explained`.
**Status:** fixed: requires human verification
**Verification:** Main checkout; focused PostgreSQL/RabbitMQ worker integration test — 1 passed.

### WR-01 (iteration 2): Production reports retain superseded planting dates

**Files modified:** `src/AgroPlan.Application/Reports/OperationsReportReader.cs`, `src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs`, `src/AgroPlan.IntegrationTests/Api/OperationsReportsTests.cs`
**Commit:** `58434ac`
**Applied fix:** Production projections now remove sowing entries superseded by linked corrections before selecting the planting date. Added unit coverage and an API regression proving JSON and CSV use the corrected active date and omit the original date.
**Status:** fixed: requires human verification
**Verification:** Main checkout; focused unit test `Production_uses_corrected_active_sowing_date` — 1 passed; focused PostgreSQL API integration test `Costs_and_production_are_farm_scoped_traceable_and_keep_units_and_currency` — 1 passed.

### WR-04 (follow-up): Expired proposals can remain in Processing

**Files modified:** `src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs`, `src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs`
**Commit:** `300f663`
**Applied fix:** On redelivery, an expired Processing proposal with an expired lease transitions to terminal `Stale` with `PROPOSAL_EXPIRED` regardless of attempt count, and its lease is cleared. Added PostgreSQL/RabbitMQ integration coverage asserting the provider is not called.
**Status:** fixed: requires human verification
**Verification:** Main checkout; focused PostgreSQL/RabbitMQ integration test `Expired_processing_schedule_proposal_becomes_stale_on_redelivery` — 1 passed.

## Verification Environment

All checks above ran in the main `develop` checkout. `git diff --check` passed before the final follow-up commit. Full solution tests were intentionally not run between individual findings; the orchestrator will run the suite after all fixes.

---

_Fixer: gsd-code-fixer_
_Iteration: 2_
