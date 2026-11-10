---
phase: 03-workforce-and-offline-operations
plan: 05
subsystem: workforce-routing
tags: [dotnet10, xunit-v3, postgres, routes, geojson, offline]

requires:
  - phase: 03-workforce-and-offline-operations
    provides: Farm-scoped worker membership, task assignment, and source task versions
provides:
  - Provider-neutral route request/proposal contract and explicit unconfigured adapter
  - Immutable, versioned route snapshots with ordered stops, optional GeoJSON, and task/parcel source versions
  - Administrator review/confirmation, stale detection, and worker-only current-route download API
affects: [workforce, offline-operations, route-provider-selection]

actuals:
  tokens: 17197
  tasks: 2
  commits: 4
  commits_measured: 4
  plan_head_before: 1ee060e5f09034feb6628f8182becc5d424a295d
  plan_head_after: 0ba690a41147e3f7fa81ecac9c08d86cf8559d54

tech-stack:
  added: []
  patterns:
    - Provider-neutral route generation returns unavailable until a provider and validated coordinates exist
    - Confirmed route versions retain ordered task ids, GeoJSON, and source task/parcel versions as JSONB
    - Current farm membership and assigned-worker scope are rechecked for every route download

key-files:
  created:
    - src/AgroPlan.Application/Abstractions/IWorkRouteProvider.cs
    - src/AgroPlan.Infrastructure/Routing/UnconfiguredWorkRouteProvider.cs
    - src/AgroPlan.Domain/Workforce/WorkRoute.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/WorkRouteConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/WorkRoutes.cs
    - src/AgroPlan.Api/Controllers/WorkRoutesController.cs
    - src/AgroPlan.UnitTests/Workforce/WorkRouteTests.cs
    - src/AgroPlan.IntegrationTests/Api/WorkRoutesTests.cs
    - .planning/phases/03-workforce-and-offline-operations/03-05-task1-red-evidence.json
    - .planning/phases/03-workforce-and-offline-operations/03-05-task2-red-evidence.json
    - .planning/phases/03-workforce-and-offline-operations/03-05-task2-integration-red-evidence.json
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs

key-decisions:
  - "Keep generation explicitly unavailable until a provider is approved and validated route-stop coordinates are available; do not infer coordinates from free-text parcel locations."
  - "Store ordered task ids and optional LineString GeoJSON only; route data contains no base maps or turn-by-turn instructions."
  - "A route proposal remains unavailable to workers until an active farm administrator confirms its latest version; changed task or parcel source versions stale the confirmed snapshot."

patterns-established:
  - "Treat external route-provider outputs as untrusted: validate the exact authorized task set and geographic coordinate bounds before persistence."
  - "Persist JSON snapshots with immutable version identity, then change only review/stale lifecycle metadata."

requirements-completed: [REQ-rf-g-12, REQ-rf-g-19]

coverage:
  - id: D1
    description: "Route generation safely reports unavailable without an approved provider and validated stop coordinates, and does not persist a route."
    requirement: REQ-rf-g-12
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkRouteTests.cs#Unconfigured_provider_reports_unavailable_without_synthetic_route_data
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkRoutesTests.cs#Route_generation_is_unavailable_until_provider_is_approved
        status: pass
    human_judgment: false
  - id: D2
    description: "Only an assigned worker can download a current confirmed route; snapshots contain ordered stops and optional geometry without maps or turn-by-turn directions."
    requirement: REQ-rf-g-19
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkRoutesTests.cs#Worker_downloads_only_confirmed_current_route_and_admin_must_confirm_replacement
        status: pass
    human_judgment: false
  - id: D3
    description: "Task or parcel source changes stale a confirmed route; a replacement version requires administrator confirmation."
    requirement: REQ-rf-g-19
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkRouteTests.cs#Route_requires_administrator_confirmation_and_becomes_stale_immutably
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkRoutesTests.cs#Parcel_change_marks_confirmed_route_stale
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-11-10
status: complete
---

# Phase 3 Plan 5: Safe Offline Route Snapshots Summary

**Farm-scoped route requests stay unavailable without an approved provider and coordinates, while reviewed route snapshots retain ordered task stops and become stale when their source tasks or parcels change.**

## Performance

- **Duration:** 20 minutes
- **Started:** 2026-11-10T22:14:27Z
- **Completed:** 2026-11-10T22:34:26Z
- **Tasks:** 2 completed
- **Files modified:** 13

## Accomplishments

- Added provider-neutral route request and result records plus an explicitly unconfigured implementation; route generation returns a clear unavailable response without saving a proposal.
- Added versioned route snapshots with ordered task ids, optional validated GeoJSON LineString, source task versions, and parcel update timestamps.
- Added administrator-only confirmation and worker-only current download. The API rechecks active membership and assignment, and marks a route stale when task or parcel source data changes.
- Added xUnit v3 unit coverage and PostgreSQL API integration coverage for unavailable generation, private offline download, administrator confirmation, task staleness, and parcel staleness.

## Task Commits

1. **Task 1 RED:** `11d6f0d` — provider-neutral contract and unconfigured adapter test.
2. **Task 1 GREEN:** `7f024e3` — provider contract and explicit unavailable implementation.
3. **Task 2 RED:** `8b8bfdd` — versioned route and API tests with validated RED evidence.
4. **Task 2 GREEN:** `0ba690a` — route persistence, review, authorization, and stale lifecycle.

## Decisions Made

- No approved geographic provider or validated parcel coordinates are available. The API exposes this as unavailable and does not infer coordinates from the parcel's free-text location.
- Offline snapshots include ordered task ids and available GeoJSON only. Base maps and turn-by-turn data are not part of the route contract.
- A newly generated route version remains a proposal until an active farm owner or administrator confirms it. Workers can retrieve only their own current confirmed version.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed untranslatable EF Core parcel lookup.**
- **Found during:** Task 2 integration verification.
- **Issue:** A LINQ expression that projected parcel ids from an in-memory task array inside an EF query could not be translated by Npgsql.
- **Fix:** Materialized the distinct parcel id array before issuing the farm-scoped database query.
- **Files modified:** `src/AgroPlan.Api/Controllers/WorkRoutesController.cs`
- **Verification:** Focused PostgreSQL route tests and the full solution test run passed.
- **Committed in:** `0ba690a`.

**2. [Rule 2 - Missing critical functionality] Blocked route generation when validated coordinates are absent.**
- **Found during:** Task 2 implementation.
- **Issue:** `Parcel.Location` is free text and cannot safely be treated as latitude/longitude; calling a future provider with missing geometry could produce an invented path.
- **Fix:** Return explicit unavailable with `parcel.coordinates` listed as missing before invoking a provider or persisting a route.
- **Files modified:** `src/AgroPlan.Api/Controllers/WorkRoutesController.cs`
- **Verification:** API integration test asserts unavailable status and no route snapshot persisted.
- **Committed in:** `0ba690a`.

**Total deviations:** 2 auto-fixed (1 Rule 1, 1 Rule 2).
**Impact on plan:** Both changes preserve safe behavior at the current data/provider boundary; no external provider or invented geographic data was introduced.

## Issues Encountered

- An early full-suite run exposed that a test-only crop catalog row affected an existing catalog assertion because the integration fixture shares PostgreSQL state. The route test now reuses the seeded `YUCA` catalog entry; the complete suite passes.
- The sandbox test host hangs without elevated execution in this Windows environment. Targeted and full Testcontainers test runs completed successfully using the approved elevated test command.

## TDD Gate Compliance

- **Task 1 RED:** `Provider_neutral_route_contract_and_unconfigured_provider_are_available` executed and failed its intended assertion because the provider contract was absent; `03-05-task1-red-evidence.json` passed `RED_EVIDENCE_OK`.
- **Task 1 GREEN:** 3 focused unit tests passed, covering explicit unavailability, no synthetic stops/geometry, and the optional GeoJSON contract.
- **Task 2 RED:** `Versioned_work_route_snapshot_domain_type_is_available` failed because the domain aggregate was absent, and `Route_generation_is_unavailable_until_provider_is_approved` reached the API and received 404; both evidence records passed `RED_EVIDENCE_OK`.
- **Task 2 GREEN:** 8 focused unit tests and 3 PostgreSQL integration tests passed for route validation, admin confirmation, per-worker access, task and parcel staleness, and unavailable generation.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~WorkRouteTests" -m:1 -nr:false` — 8 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~WorkRoutesTests" -m:1 -nr:false` — 3 passed with PostgreSQL Testcontainers.
- `dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false` — full solution passed: 119 unit tests and 45 integration tests (164 total).
- API and Worker compiled successfully as part of the solution run.

## Self-Check: PASSED

- Summary, route API, domain, provider, persistence configuration/migration, and test files exist at the recorded paths.
- The four task commits are ancestors of `develop`; `plan_head_before` and `plan_head_after` were measured from the per-plan ledger.
