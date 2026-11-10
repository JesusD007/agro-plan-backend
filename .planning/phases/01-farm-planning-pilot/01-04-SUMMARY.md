---
phase: 01-farm-planning-pilot
plan: 04
subsystem: crop-catalog-and-cycles
tags: [dotnet, ef-core, postgres, crops, farm-authorization, agronomic-provenance]
requires:
  - phase: 01-03
    provides: Farm membership authorization, farms, and tenant-scoped parcels.
provides:
  - Five pilot crop identities with catalog provenance and explicit missing agronomic fields.
  - Agronomic parameter metadata model with validation source, region, and validity range.
  - Farm-scoped crop cycle create/read/update API with same-farm parcel constraints.
affects: [sowing-plans, planting-frame-proposals]
actuals:
  tokens: 8081
  tasks: 2
  commits: 3
plan_head_before: ecee55a8469cb3f4546522f61f754393daa83f2a
plan_head_after: 12c56d99b2f56f43c5e7775c7b3ce9f4df4a8d2c
tech-stack:
  added: []
  patterns: [provenance-backed-parameters, missing-input-markers, composite-tenant-foreign-key]
key-files:
  created:
    - src/AgroPlan.Domain/Crops/CropCatalogEntry.cs
    - src/AgroPlan.Domain/Crops/AgronomicParameter.cs
    - src/AgroPlan.Domain/Crops/CropCycle.cs
    - src/AgroPlan.Application/Crops/CropCatalogService.cs
    - src/AgroPlan.Application/Crops/ICropCatalogRepository.cs
    - src/AgroPlan.Api/Controllers/CropCyclesController.cs
    - src/AgroPlan.Infrastructure/Persistence/CropCatalogRepository.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/CropCatalogConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/CropCycleConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/CropCatalog.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/CropCycles.cs
  modified:
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Api/Program.cs
key-decisions:
  - "Seed only the five user-approved crop identities; no numeric or regional agronomic values are asserted without validation sources."
  - "Region-specific validated parameters are not treated as applicable while farm locations are not normalized to agronomic zones."
  - "Crop cycles reference parcels with a composite (FarmId, ParcelId) foreign key to prevent cross-farm associations at the database boundary."
patterns-established:
  - "Catalog response reports required parameter names that are absent, unvalidated, expired, or tied to an unmatched region."
  - "The Application layer reads catalog entities through a repository abstraction; the Infrastructure layer owns EF Core queries."
requirements-completed: [REQ-pilot-crop-cycle, REQ-rf-g-38]
coverage:
  - id: D1
    description: Five crop identities expose parameter provenance and missing-input status without fabricated agronomic values.
    requirement: REQ-rf-g-38
    verification:
      - kind: other
        ref: dotnet build src/AgroPlan.sln --no-restore --verbosity minimal -m:1
        status: pass
      - kind: other
        ref: Static review of catalog migration seed and applicability filtering
        status: pass
    human_judgment: true
    rationale: The response and SQL migration were not exercised against a live database; agronomic source validation requires an agronomist.
  - id: D2
    description: Authorized users can create, read, and update crop cycles associated only with parcels in the same farm.
    requirement: REQ-pilot-crop-cycle
    verification:
      - kind: other
        ref: dotnet build src/AgroPlan.sln --no-restore --verbosity minimal -m:1
        status: pass
      - kind: other
        ref: Static review of farm-scoped controller queries and composite database foreign key
        status: pass
    human_judgment: true
    rationale: Runtime API requests and database constraint behavior were not exercised; no tests were added or run.
duration: 9m
completed: 2026-11-10
status: complete
---

# Phase 1 Plan 4: Crop Catalog and Cycle Summary

**Five pilot crops are available with provenance-aware missing inputs, and crop cycles are linked to farm-owned parcels.**

## Performance

- **Duration:** about 9 minutes
- **Started:** 2026-11-10T04:16:00Z
- **Completed:** 2026-11-10T04:25:00Z
- **Tasks:** 2
- **Files modified:** 13

## Accomplishments

- Added plátano, yuca, batata, arroz, and habichuela as catalog identities, sourced only from the user-approved pilot list.
- Added agronomic parameter validation metadata and returns missing markers for variety, planting window, spacing, density, cycle duration, growth stages, water requirement, and zone suitability.
- Added authenticated crop-cycle catalog/create/list/read/update endpoints; each request uses current farm membership authorization and same-farm parcel checks.
- Added database constraints and migrations for crop catalog, agronomic parameters, and crop cycles.

## Task Commits

1. **Task 1: Define five pilot catalog entries with provenance and missing-input state** — `b70cca9`
2. **Task 2: Create and retrieve crop cycles under authorized parcels** — `c159701`
3. **Auto-fix: Keep regional agronomic parameters unavailable until a farm zone matches** — `12c56d9`

## Decisions Made

- The five approved crop names are seeded without numeric agronomic parameters; source, validation, region, and validity fields remain explicit.
- Region-specific numeric values are withheld until locations are normalized to agronomic zones.
- A composite tenant foreign key protects crop-cycle-to-parcel relationships even if application validation is bypassed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added a repository boundary and limited parameter applicability by region**
- **Found during:** Task 1 and Task 2
- **Issue:** The Application service needed an Infrastructure-backed catalog read without depending directly on EF Core, and a region-specific parameter cannot be safely applied when the farm's agronomic zone is unknown.
- **Fix:** Added `ICropCatalogRepository` with an EF-backed implementation; regional rules remain missing until an explicit region match exists. The parameter index permits multiple regional and validity records.
- **Files modified:** `src/AgroPlan.Application/Crops/`, `src/AgroPlan.Infrastructure/Persistence/CropCatalogRepository.cs`, `src/AgroPlan.Application/Crops/CropCatalogService.cs`, `src/AgroPlan.Infrastructure/Persistence/Configurations/CropCatalogConfiguration.cs`, `src/AgroPlan.Infrastructure/Persistence/Migrations/CropCatalog.cs`
- **Verification:** .NET 10 solution build passed with zero warnings and zero errors; reviewed the seed and filter statically.
- **Committed in:** `b70cca9`, `12c56d9`

**Total deviations:** 1 auto-fixed (missing critical behavior and safe layering).
**Impact on plan:** Required for clean architecture and to avoid applying region-specific agronomic data without a validated farm-zone match.

## Issues Encountered

- No live database or agronomist-validated crop parameters were available. The implementation returns explicit missing fields and does not seed recommendation values.

## Known Stubs

| File | Reason |
|---|---|
| `src/AgroPlan.Infrastructure/Persistence/Migrations/CropCatalog.cs` | No agronomic parameter rows are seeded until validated sources and regional applicability are provided; the catalog API marks those fields missing. |

## Verification

- `dotnet build src/AgroPlan.sln --no-restore --verbosity minimal -m:1` — passed with 0 warnings and 0 errors.
- No tests were added or run. Manual API and live database scenarios were not executed.
- Migrations were not applied to a database.

## Next Phase Readiness

- Plan 01-05 can consume the catalog and cycle records for sowing job inputs.
- No climate or agronomic recommendation is enabled by this plan; climate provider selection and agronomic source validation remain production gates.

---
*Phase: 01-farm-planning-pilot*
*Completed: 2026-11-10*
