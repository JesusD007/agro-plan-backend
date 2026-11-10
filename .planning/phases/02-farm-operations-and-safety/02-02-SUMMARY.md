---
phase: 02-farm-operations-and-safety
plan: 02
subsystem: api
tags: [aspnetcore, efcore, postgresql, xunit-v3, immutable-operations, frequency-rules]
requires:
  - phase: 02-01
    provides: Farm-scoped immutable operational record and correction patterns
provides:
  - Farm-scoped immutable irrigation entries with linked corrections
  - Structured input application records preserving product/use identity and original units
  - Exact validated application-frequency assessment with provenance and safe unavailable states
affects: [harvest-safety, crop-calendars, operational-reports]
actuals:
  tokens: 13536
  tasks: 2
  commits: 3
commits: 3
plan_head_before: 6be6be8036b3c7303ba82da05d3be6d2a35b12dd
plan_head_after: b0ef3b9f1528bd68438a9812a74b720b5336abd0
tech-stack:
  added: []
  patterns: ["Append-only farm event tables with PostgreSQL mutation guards", "Exact validated rule evaluation with explicit unavailable status"]
key-files:
  created:
    - src/AgroPlan.Domain/Operations/IrrigationEntry.cs
    - src/AgroPlan.Domain/Operations/InputApplicationEntry.cs
    - src/AgroPlan.Api/Controllers/IrrigationEntriesController.cs
    - src/AgroPlan.Api/Controllers/InputApplicationsController.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/IrrigationEntries.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/InputApplications.cs
    - src/AgroPlan.UnitTests/Operations/IrrigationEntryTests.cs
    - src/AgroPlan.UnitTests/Operations/InputApplicationEntryTests.cs
  modified:
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
key-decisions:
  - "No client-supplied PHI is stored or trusted; label evidence is resolved separately by the harvest-safety plan."
  - "Frequency status is available only from one exact, current, validated rule for product, formulation, crop, use, and jurisdiction."
metrics:
  duration: 8min
  completed: 2026-11-10
status: complete
duration: 8min
completed: 2026-11-10
requirements-completed: [REQ-rf-g-31, REQ-rf-g-32]
coverage:
  - id: D1
    description: Producers append/list irrigation history and correct it through a linked immutable record.
    requirement: REQ-rf-g-31
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Operations/IrrigationEntryTests.cs
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/IrrigationEntriesTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Input applications retain product/use identity and original units; frequency assessment is sourced or explicitly unavailable.
    requirement: REQ-rf-g-32
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Operations/InputApplicationEntryTests.cs
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/InputApplicationsTests.cs
        status: pass
    human_judgment: false
---

# Phase 2 Plan 02: Irrigation and Input Applications Summary

**Farm-scoped irrigation and input application records preserve correction history, original units, and evidence-backed application-frequency statuses.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-11-10T13:42:00Z
- **Completed:** 2026-11-10T13:49:00Z
- **Tasks:** 2
- **Files modified:** 13

## Accomplishments

- Added farm-scoped append/list/correction endpoints for irrigation, with water method/source, same-farm references, and PostgreSQL mutation protection.
- Added structured product application records with crop/use/product identity, method, amount/unit, and immutable linked corrections.
- Added validated frequency rules keyed by product, formulation, crop/use and jurisdiction. Unknown, stale, unvalidated, or ambiguous rule sets report unavailable with an evidence reason; no limit is inferred.
- Prevented client-supplied PHI values from entering the application contract; harvest safety remains owned by the exact-label safety evaluator.

## Task Commits

1. **Task 1: Record farm irrigation with original source fields** — `a8de38d` (`feat`)
2. **Task 2: Record input applications for exact product and crop use** — `253a4de` (`feat`)
3. **Task 2 test follow-up: Propagate xUnit cancellation tokens** — `b0ef3b9` (`test`)

## Files Created/Modified

- `src/AgroPlan.Domain/Operations/IrrigationEntry.cs` — immutable irrigation domain model.
- `src/AgroPlan.Api/Controllers/IrrigationEntriesController.cs` — farm-authorized irrigation routes.
- `src/AgroPlan.Domain/Operations/InputApplicationEntry.cs` — application and validated frequency rule models/evaluator.
- `src/AgroPlan.Api/Controllers/InputApplicationsController.cs` — append/list/correction endpoints and assessments.
- `src/AgroPlan.Infrastructure/Persistence/Configurations/` — EF mappings with same-farm keys and query indexes.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/` — immutable operation tables and frequency-rule storage.
- `src/AgroPlan.UnitTests/Operations/` and `src/AgroPlan.IntegrationTests/Api/` — xUnit v3 domain and PostgreSQL API coverage.
- `src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs` — operation and frequency-rule DbSets.

## Decisions Made

- Product label PHI is not accepted as application input authority. The separate harvest evaluator must resolve current validated label evidence before allowing a harvest.
- Frequency assessment fails closed to unavailable if there is no single exact validated rule effective for the Dominican jurisdiction at assessment time.
- Corrections remain linked immutable operations and do not double-count superseded application records in the frequency evaluator.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Prevented ambiguous or foreign-jurisdiction frequency rules from driving a status**
- **Found during:** Task 2 (Record input applications for exact product and crop use)
- **Issue:** A duplicate active rule could throw, and the domain assessor did not independently check jurisdiction.
- **Fix:** Require exactly one active validated rule for the configured jurisdiction; otherwise return unavailable with an explicit reason.
- **Files modified:** `src/AgroPlan.Domain/Operations/InputApplicationEntry.cs`
- **Verification:** xUnit frequency tests and API suite pass.
- **Committed in:** `253a4de`

**2. [Rule 2 - Missing Critical] Reject omitted method and default application dates**
- **Found during:** Tasks 1–2
- **Issue:** Non-nullable enum/date request values could silently default when omitted.
- **Fix:** Make API methods nullable and reject missing values; validate non-default occurrence dates in the domain.
- **Files modified:** Irrigation and input application API/domain files.
- **Verification:** Full build and suites pass.
- **Committed in:** `a8de38d`, `253a4de`

**3. [Rule 1 - Bug] Count only the latest linked application correction when assessing frequency**
- **Found during:** Task 2
- **Issue:** A corrected immutable row and its replacement could otherwise count as two application events.
- **Fix:** Exclude entries superseded by a linked correction when counting in-period applications.
- **Files modified:** `src/AgroPlan.Domain/Operations/InputApplicationEntry.cs`
- **Verification:** `Correction_does_not_double_count_superseded_application` passes.
- **Committed in:** `253a4de`

---

**Total deviations:** 3 auto-fixed (2 Rule 1, 1 Rule 2)
**Impact on plan:** These fixes strengthen safe handling of missing or conflicting inputs without adding provider dependencies or expanding scope.

## Issues Encountered

- Initial unit runs exposed test data using different farms for frequency-count scenarios; fixtures were aligned and rerun.
- Initial API assertions showed enum status casing differed from the intended lower-case API values; response values were normalized and all API tests rerun.
- xUnit analyzer warnings remain in existing API tests (including cancellation-token guidance); they do not fail the build.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Harvest-safety work can consume product registration, formulation, crop/use and application event data while resolving PHI from its own validated, versioned label source.
- No application-frequency limits are populated by this plan; validated rules must be sourced and loaded before frequency statuses can be other than unavailable.

## Self-Check: PASSED

- All files created by both tasks exist; build succeeds with no errors.
- Task commits `a8de38d`, `253a4de`, and `b0ef3b9` are reachable from `develop`.
- Unit tests: 63 passed, 0 failed, 0 skipped.
- API integration tests: 13 passed, 0 failed, 0 skipped.

---
*Phase: 02-farm-operations-and-safety*
*Completed: 2026-11-10*
