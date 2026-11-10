---
phase: 02-farm-operations-and-safety
plan: 04
subsystem: api
tags: [dotnet, aspnetcore, postgresql, xunit-v3, phi, harvest-safety]
requires:
  - phase: 02-02
    provides: Farm-scoped immutable input applications with exact product/use identity
provides:
  - Exact, versioned label evidence and deterministic fail-closed harvest eligibility
  - Farm-scoped harvest assessment and registration with append-only corrections
affects: [crop-calendars, operations-reports, advisor-access]
actuals:
  tokens: 10605
  tasks: 2
  commits: 3
plan_head_before: 929e0cc6e8870118957e6903333c4d2ea2becbef
plan_head_after: 62ee4bf40a62bcf603775bac092f57e41672bccf
tech-stack:
  added: []
  patterns: [Exact product/formulation/crop/use matching, fail-closed PHI evaluation, immutable evidence and harvest records]
key-files:
  created:
    - src/AgroPlan.Domain/Safety/ValidatedProductLabel.cs
    - src/AgroPlan.Domain/Operations/HarvestEntry.cs
    - src/AgroPlan.Application/Safety/HarvestEligibilityEvaluator.cs
    - src/AgroPlan.Api/Controllers/HarvestRecordsController.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/HarvestSafety.cs
    - src/AgroPlan.UnitTests/Safety/HarvestEligibilityEvaluatorTests.cs
    - src/AgroPlan.IntegrationTests/Api/HarvestSafetyTests.cs
  modified:
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
requirements-completed: [REQ-rf-g-07, REQ-rf-g-42]
coverage:
  - id: D1
    description: Exact current validated PHI evidence determines earliest safe harvest time; missing, expired, mismatched, ambiguous, or unvalidated evidence blocks.
    requirement: REQ-rf-g-07
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Safety/HarvestEligibilityEvaluatorTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Farm-scoped harvest registration persists only eligible harvests, with provenance and immutable linked corrections.
    requirement: REQ-rf-g-42
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/HarvestSafetyTests.cs
        status: pass
    human_judgment: false
tech-stack-added: []
decisions:
  - "Only an exact validated product, formulation, crop, intended-use and jurisdiction label with a PHI can authorize harvest."
  - "Keep PHI separate from MRL and worker re-entry intervals; neither can substitute for missing PHI."
  - "Label evidence and harvest records are immutable; corrections append a linked record."
duration: 25m
completed: 2026-11-10
status: complete
---

# Phase 2 Plan 4: Harvest Safety Summary

**Harvest registration now requires traceable validated pesticide-label PHI evidence for every recorded application.**

## Performance

- **Duration:** 25m
- **Started:** 2026-11-10T14:17:00Z
- **Completed:** 2026-11-10T14:42:24Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments

- Added validated, versioned product-label facts that keep PHI distinct from residue limits and worker re-entry periods.
- Added a deterministic farm-scoped eligibility assessment that blocks missing, stale, mismatched, ambiguous, and unvalidated evidence, as well as harvests before the latest safe date.
- Added immutable harvest entries, linked corrections, evidence snapshots, farm-bound database references, and database mutation guards.

## Task Commits

1. **Task 1: Evaluate harvest safety against exact versioned label evidence** — `03b7e01` (`test(02-04): cover validated harvest interval assessment`)
2. **Task 2: Gate immutable harvest registration and persist assessed provenance** — `b41de78` (`feat(02-04): gate harvest records on validated PHI evidence`)
3. **Task 2 verification follow-up: Assert mutation/deletion and cross-farm denial** — `62ee4bf` (`test(02-04): assert harvest immutability and farm scope`)

**Measured plan commits:** 3 (`929e0cc6e8870118957e6903333c4d2ea2becbef` → `62ee4bf40a62bcf603775bac092f57e41672bccf`).

## Verification

- `dotnet build src/AgroPlan.sln --no-restore -m:1` — passed, 0 errors.
- Focused unit tests — 7 passed, 0 failed, 0 skipped.
- Focused PostgreSQL API integration tests — 3 passed, 0 failed, 0 skipped.
- Builds report existing NU1900 vulnerability-feed connectivity warnings and xUnit analyzer warnings; no new compiler errors.

## Deviations from Plan

- The task flags requested TDD. Tests and implementation were committed in two task-scoped commits, but no separate RED evidence artifact was recorded. The final xUnit behavior checks pass; the implementation did not follow the complete RED/GREEN evidence sequence.
- Added a crop-cycle identity check so an application claiming a different crop cannot match a verified label and authorize harvest.
- Made validated label versions immutable in PostgreSQL so corrections require a new versioned row and existing harvest provenance remains stable.

## Issues Encountered

- Shared PostgreSQL integration state can contain multiple valid labels for the same product identity. The evaluator correctly blocks ambiguous matching evidence; test fixtures now use unique registration identities.
- The sandboxed testhost failed to connect; focused xUnit runs passed outside the sandbox.

## User Setup Required

No external provider setup is required. A validated label ingestion/source process remains a prerequisite before real pesticide rules can authorize harvest; without those rows, the API stays fail-closed.

## Next Phase Readiness

Calendar and report plans can now depend on deterministic PHI assessment. Production harvests remain blocked until an approved process supplies exact current validated label evidence.

---
*Phase: 02-farm-operations-and-safety*
*Completed: 2026-11-10*

## Self-Check: PASSED

- Required domain, evaluator, API, persistence, migration, and test files exist.
- Task commits `03b7e01`, `b41de78`, and `62ee4bf` are reachable from `develop`.
- Frontmatter validates against the GSD summary schema.
