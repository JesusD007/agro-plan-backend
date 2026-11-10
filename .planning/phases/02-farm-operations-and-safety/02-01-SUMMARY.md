---
phase: 02-farm-operations-and-safety
plan: 01
subsystem: api
tags: [aspnet-core, ef-core, postgresql, xunit-v3, immutable-records]
requires:
  - phase: 01-farm-planning-pilot
    provides: Farm membership authorization, parcel/crop-cycle persistence, API and PostgreSQL test infrastructure
provides:
  - Farm-scoped expense, labor and material operation records
  - Immutable linked corrections preserving source units and currencies
affects: [farm-operations, safety, reports, advisors]
actuals:
  tokens: 6845
  tasks: 2
  commits: 2
plan_head_before: f1cd9954657324e214fe143d82b60c260271745c
plan_head_after: 64c8bea95259173e9c5902dfe202ecb78ae4ade6
commits: 2
tech-stack:
  added: []
  patterns: [farm-scoped controllers, append-only corrections, PostgreSQL immutability trigger]
key-files:
  created:
    - src/AgroPlan.Domain/Operations/ExpenseEntry.cs
    - src/AgroPlan.Api/Controllers/ExpenseEntriesController.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/ExpenseEntries.cs
    - src/AgroPlan.UnitTests/Operations/ExpenseEntryTests.cs
    - src/AgroPlan.IntegrationTests/Api/ExpenseEntriesTests.cs
  modified:
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
key-decisions:
  - "Every monetary amount carries an explicit three-letter currency code; no default conversion is applied."
  - "Corrections append a new row linked to the original; PostgreSQL rejects UPDATE and DELETE."
patterns-established:
  - "Farm-scoped operation queries filter by farm and validate parcel/cycle ownership."
  - "A correction preserves farm, parcel, crop cycle and category while recording actor, time and reason."
requirements-completed: [REQ-rf-g-02]
coverage:
  - id: D1
    description: Farm-scoped API records expenses, labor and materials while preserving currency and entered quantity units.
    requirement: REQ-rf-g-02
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/ExpenseEntriesTests.cs#Append_list_correction_and_cross_farm_authorization_preserve_immutable_history
        status: pass
      - kind: integration
        ref: dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"
        status: pass
    human_judgment: false
  - id: D2
    description: Domain validation rejects invalid currency, amount, quantity/unit combinations and creates immutable linked corrections.
    requirement: REQ-rf-g-02
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Operations/ExpenseEntryTests.cs
        status: pass
      - kind: unit
        ref: dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj
        status: pass
    human_judgment: false
duration: 9min
completed: 2026-11-10
status: complete
---

# Phase 2 Plan 1: Farm Operations Record Summary

**Farm-scoped expense, labor and material records with immutable correction history and source currency/unit preservation**

## Performance

- **Duration:** 9 min
- **Started:** 2026-11-10T13:25:00Z
- **Completed:** 2026-11-10T13:34:08Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments

- Added farm-scoped append/list endpoints for expenses, labor and material costs, validating active membership through the existing policy and verifying parcel/crop-cycle ownership.
- Added linked correction operations that preserve the source record and its crop-cycle and category context.
- Enforced append-only storage with same-farm composite references and a PostgreSQL trigger that rejects row updates and deletes.
- Added xUnit v3 validation and PostgreSQL API coverage for categories, currencies, entered units, cross-farm denial, correction linkage and mutation/deletion rejection.

## Task Commits

1. **Task 1: End-to-end farm expense, labor and material record** - `985a17b` (feat)
2. **Task 2: Unit-test operation validation and correction history** - `64c8bea` (test)

**Plan metadata:** Included with this plan's completion commit.

## Files Created/Modified

- `src/AgroPlan.Domain/Operations/ExpenseEntry.cs` - Validated immutable operation cost model.
- `src/AgroPlan.Application/UseCases/AppendExpenseEntry.cs` - Domain factory for new entries.
- `src/AgroPlan.Application/UseCases/CorrectExpenseEntry.cs` - Appends linked correction entries.
- `src/AgroPlan.Api/Controllers/ExpenseEntriesController.cs` - Farm-scoped API routes and ownership checks.
- `src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs` - Registers the expense entries set.
- `src/AgroPlan.Infrastructure/Persistence/Configurations/ExpenseEntryConfiguration.cs` - EF constraints and farm-bound references.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/ExpenseEntries.cs` - Table, indexes and database immutability trigger.
- `src/AgroPlan.UnitTests/Operations/ExpenseEntryTests.cs` - Domain and correction unit tests.
- `src/AgroPlan.IntegrationTests/Api/ExpenseEntriesTests.cs` - PostgreSQL-backed endpoint and security tests.

## Decisions Made

- Amounts require their own ISO-style three-letter currency code; no currency default or implicit conversion is used.
- Quantity and unit must be supplied together; the entered unit is retained without conversion.
- Corrections preserve the original row and capture the actor, timestamp, reason and corrected values in a new linked row.

## Deviations from Plan

None - plan executed as specified.

## Issues Encountered

- The first build attempt could not restore packages because the sandbox could not resolve NuGet. Re-running with authorized network access restored packages successfully.
- The first sandboxed xUnit invocation could not connect to the .NET testhost. The unit and integration suites passed when run with the approved elevated execution path.
- The GSD runtime did not expose the executable descriptor required for isolated worktrees; this plan ran as one sequential GSD executor on `develop`.

## User Setup Required

None - no new external service configuration required.

## Next Phase Readiness

The operation-entry foundation is available for irrigation/input applications and later reports. Existing currency and unit values stay explicit; downstream calculations must not aggregate currencies or convert quantities without a validated source.

---
*Phase: 02-farm-operations-and-safety*
*Completed: 2026-11-10*

## Self-Check: PASSED

- All created source and test files exist.
- Task commits `985a17b` and `64c8bea` are ancestors of the current branch head.
- Unit tests passed: 53 passed, 0 failed, 0 skipped.
- API integration tests passed: 10 passed, 0 failed, 0 skipped.
