---
phase: 02-farm-operations-and-safety
plan: 07
subsystem: api
tags: [aspnetcore, postgres, authorization, advisor-access, xunit-v3]
requires:
  - phase: 02-farm-operations-and-safety
    provides: Farm-scoped operational records and report projections
provides:
  - Bounded producer-managed advisor grants with read-only category scopes
  - Per-request database authorization with immediate revocation and persisted last-use time
  - Farm-scoped advisor JSON and CSV reports
affects: [advisor-access, authorization, reporting, phase-3]
actuals:
  tokens: 10262
  tasks: 2
  commits: 2
plan_head_before: 2eaa713
plan_head_after: 612553643f0060b342d7afcabd43b4466c1c6c7f
tech-stack:
  added: []
  patterns: [per-request resource authorization, bounded persisted grants, explicit read-only report scopes]
key-files:
  created:
    - src/AgroPlan.Domain/AdvisoryAccess/AdvisorAccessGrant.cs
    - src/AgroPlan.Application/AdvisoryAccess/AdvisorGrantService.cs
    - src/AgroPlan.Api/Authorization/AdvisorReportAuthorizationHandler.cs
    - src/AgroPlan.Api/Controllers/AdvisorAccessController.cs
    - src/AgroPlan.Api/Controllers/AdvisorReportsController.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/AdvisorAccessGrants.cs
    - src/AgroPlan.UnitTests/AdvisoryAccess/AdvisorGrantTests.cs
    - src/AgroPlan.IntegrationTests/Api/AdvisorAccessTests.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/AdvisorAccessGrantConfiguration.cs
key-decisions:
  - "Advisor scopes are report-only enum values; no write or account-wide scope exists."
  - "Grant validity is bounded to 90 days, and current grant state is loaded from PostgreSQL for each report request."
  - "CSV endpoints run the same farm, scope, expiry, and revocation authorization as JSON endpoints."
patterns-established:
  - "Advisor grants remain separate from farm membership and are granted/revoked only by the active farm owner."
  - "Persist LastUsedAt after reloading the current valid grant for an authorized request."
requirements-completed: [REQ-rf-g-37]
coverage:
  - id: D1
    description: Producer-managed, read-only advisor grant is farm-scoped, category-scoped, purpose-bound, and time-limited.
    requirement: REQ-rf-g-37
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/AdvisoryAccess/AdvisorGrantTests.cs
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/AdvisorAccessTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Report requests reload current grant state and deny expired, revoked, cross-farm, out-of-scope, and write attempts, including CSV.
    requirement: REQ-rf-g-37
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/AdvisorAccessTests.cs
        status: pass
    human_judgment: false
duration: 20min
completed: 2026-11-10
status: complete
---

# Phase 2 Plan 07: Advisor Access Summary

**Producer-controlled grants now provide bounded, farm-scoped, read-only report access with immediate revocation.**

## Performance

- **Duration:** 20 min
- **Tasks:** 2
- **Files modified:** 12

## Accomplishments

- Added persisted advisor grant lifecycle with owner-only creation/revocation, explicit report scopes, purpose, validity, revoke actor/time, and last-use metadata.
- Added database-backed authorization that checks farm, advisor, requested category, start/end, and revocation state on every JSON and CSV report request.
- Added advisor costs and production report endpoints reusing existing projections and farm-filtered queries.

## Task Commits

1. **Task 1: Create and audit producer-authorized advisor grants** — `2d681d2`
2. **Task 2: Enforce live read-only scope on advisor data and report requests** — `6125536`

## Decisions Made

- Keep advisor grants distinct from owner/administrator membership.
- Limit grant validity to 90 days and permit only the `Costs` and `Production` read scopes.
- Update `LastUsedAt` after reloading a valid current grant during authorization.

## Deviations from Plan

- The grant domain and endpoint implementation preceded the unit test run; separate RED/GREEN commits were not recorded. The final unit and integration tests passed.
- To keep domain ownership simple, report scopes in this plan cover costs and production. Other advisory categories require an explicit additional scope and endpoint before an advisor can read them.

## Issues Encountered

- The sandboxed test runner could not connect to its child testhost process. The focused and full test commands succeeded when run with the required testhost/Testcontainers process access.
- NuGet emitted `NU1900` warnings because `api.nuget.org` was unreachable while retrieving vulnerability metadata.

## User Setup Required

None.

## Next Phase Readiness

Advisor reads are immediately revocable and remain scoped to explicit reports. The full xUnit suite passed with 85 unit tests and 25 PostgreSQL integration tests. No production provider or new package was introduced.

## Self-Check: PASSED

- All created implementation and test files exist.
- Plan task commits `2d681d2` and `6125536` are present on `develop`.
- Full solution verification passed: 85 unit tests and 25 integration tests, with no failures or skips.

---
*Phase: 02-farm-operations-and-safety*
*Completed: 2026-11-10*
