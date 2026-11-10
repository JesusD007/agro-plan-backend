---
phase: 02-farm-operations-and-safety
plan: 06
subsystem: reporting
tags: [aspnetcore, postgresql, xunit-v3, csv, farm-scope]
requires:
  - phase: 02-farm-operations-and-safety
    provides: Immutable farm operation costs, PHI-gated harvest records, and current membership authorization
provides:
  - Farm-scoped cost and production report projections with source record identifiers
  - Currency and original-unit grouping with unavailable cost-per-quintal reasons
  - Formula-safe CSV exports for costs and production
affects: [advisor-reports, phase-2-integration]
actuals:
  tokens: 6513
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns: [Pure report projections over farm-filtered EF query results, Formula-safe CSV escaping]
key-files:
  created:
    - src/AgroPlan.Application/Reports/OperationsReportReader.cs
    - src/AgroPlan.Application/Reports/CostReport.cs
    - src/AgroPlan.Application/Reports/ProductionReport.cs
    - src/AgroPlan.Api/Controllers/OperationsReportsController.cs
    - src/AgroPlan.Api/Reports/CsvReportWriter.cs
    - src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs
    - src/AgroPlan.IntegrationTests/Api/OperationsReportsTests.cs
  modified: []
key-decisions:
  - "Costs remain grouped by currency; mixed-currency totals require an explicit validated exchange-rate source."
  - "Production quantities remain grouped by original unit; cost-per-quintal stays unavailable until validated crop and conversion evidence exists."
  - "Production planting dates come from confirmed sowing logbook records; estimates or planned dates are not reported as actual planting."
patterns-established:
  - "Reporting queries filter by farm before projection and include source record identifiers."
  - "CSV formula-leading cells are prefixed safely and quoted; numeric output uses invariant culture."
requirements-completed: [REQ-rf-g-03, REQ-rf-g-15, REQ-rf-g-36]
coverage:
  - id: D1
    description: Cost and production summaries preserve provenance and group original currencies and units.
    requirement: REQ-rf-g-03
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Farm-scoped JSON report APIs and CSV exports preserve scope and resist spreadsheet formula injection.
    requirement: REQ-rf-g-36
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/OperationsReportsTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Cost-per-quintal stays unavailable until crop-specific quintal and conversion evidence is validated.
    requirement: REQ-rf-g-15
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs
        status: pass
    human_judgment: false
duration: 16min
completed: 2026-11-10
status: complete
commits: 2
plan_head_before: f86d155510235c1a7d7b1bc67324dded215988c5
plan_head_after: 50fb9f39d480427c9c9080c8367a2bc05a4bc0b6
---

# Phase 2 Plan 6: Cost and Production Reports Summary

**Read-only farm reports now expose traceable costs and production without mixing currencies or inferring unsupported unit conversions.**

## Performance

- **Duration:** 16 minutes
- **Started:** 2026-11-10T15:08:00Z
- **Completed:** 2026-11-10T15:24:22Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- Added farm/cycle/date-filtered cost and production JSON reports with source-entry identifiers and correction-aware totals.
- Preserved each amount's currency and each quantity's entered unit; cost-per-quintal reports a typed not-computable status while validated conversion evidence is absent.
- Added protected CSV exports that include original units, source IDs, confirmed planting dates and spreadsheet formula escaping.

## Task Commits

1. **Task 1: Calculate auditable farm/cycle cost and yield summaries** — `f885a1b` (`feat`)
2. **Task 2: Expose read-only CSV cost, yield and production exports** — `50fb9f3` (`feat`)

## Verification

- `dotnet build src/AgroPlan.sln --no-restore -m:1` — passed, 0 errors.
- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1` — passed, 81/81.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Api" -m:1` — passed, 23/23.
- `dotnet test src/AgroPlan.sln --no-restore -m:1` — passed, 81 unit + 23 integration tests, 0 failures/skips.

All commands required escalation because the sandboxed vstest runner could not connect to its local testhost; they passed outside the sandbox. NuGet emitted NU1900 warnings because its vulnerability-data feed was unreachable.

## Deviations from Plan

- The tasks carried `tdd="true"`, but the implementation was not recorded in a separate RED/GREEN commit sequence. The final unit and integration suites pass; the commits remain separated by task.
- The first sandboxed xUnit invocation timed out connecting to testhost. Re-running the suites with the approved unsandboxed command completed successfully.

**Total deviations:** 2. **Impact:** No product scope was reduced; verification passed after correcting the test invocation environment.

## Issues Encountered

- One unit assertion initially expected a correction to replace an amount but omitted the separate valid expense line from the total. The expectation was corrected to include the unchanged entry and the correction; the suite then passed.

## Next Phase Readiness

- Cost and production data are available for the advisor-report authorization plan.
- Provider-backed exchange rates and validated quintal definitions remain unavailable and are not synthesized.

## Self-Check: PASSED

- All seven declared implementation files exist.
- Commits `f885a1b` and `50fb9f3` are present on `develop`.
- The summary commit range records 2 commits from `f86d155510235c1a7d7b1bc67324dded215988c5` through `50fb9f39d480427c9c9080c8367a2bc05a4bc0b6`.
