---
phase: 01-farm-planning-pilot
plan: 03
subsystem: farm-management
tags: [aspnet-core, postgres, ef-core, farm-authorization, parcels]
requires:
  - phase: 01-01
    provides: API foundation, identity, and farm membership
  - phase: 01-02
    provides: Account sessions and support recovery
provides:
  - Request-time active farm role authorization through IFarmAuthorization
  - Owner-controlled administrator grants and revocations with an audit trail
  - Tenant-scoped farm and parcel CRUD with relational constraints
affects: [crop-cycles, sowing-plans, planting-frames, logbook]
actuals:
  tokens: 7879
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns: [request-time-database-membership, parent-scoped-resource-queries, append-only-membership-audit]
key-files:
  created:
    - src/AgroPlan.Api/Controllers/MembershipsController.cs
    - src/AgroPlan.Api/Controllers/ParcelsController.cs
    - src/AgroPlan.Application/Abstractions/IFarmAuthorization.cs
    - src/AgroPlan.Domain/Farms/Parcel.cs
    - src/AgroPlan.Domain/Farms/FarmMembershipAuditEntry.cs
    - src/AgroPlan.Infrastructure/Persistence/FarmAuthorizationService.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/ParcelConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmMembershipAuditEntryConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/FarmAdministration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/FarmParcels.cs
  modified:
    - src/AgroPlan.Api/Authorization/FarmMembershipAuthorizationHandler.cs
    - src/AgroPlan.Api/Controllers/FarmsController.cs
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Domain/Farms/Farm.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmConfiguration.cs
key-decisions:
  - "Only farm owners can grant or revoke farm-administrator memberships; every change records actor, target, action, and timestamp."
  - "Parcel reads and writes constrain both farm id and parcel id; the schema includes a tenant-scoped alternate key for future cross-record references."
patterns-established:
  - "Farm role authorization resolves active membership from PostgreSQL on every scoped request."
  - "Parcel endpoints resolve resources beneath the route's farm id instead of trusting a parcel id alone."
requirements-completed: [REQ-pilot-farms-parcels, REQ-rf-g-01]
coverage:
  - id: D1
    description: Owners can grant and revoke administrator membership with an audit record.
    requirement: REQ-pilot-farms-parcels
    verification: []
    human_judgment: true
    rationale: Runtime authorization, database transitions, and audit behavior were not exercised against a running PostgreSQL service.
  - id: D2
    description: Farm and parcel CRUD routes constrain access and lookups to current farm membership and parent ids.
    requirement: REQ-rf-g-01
    verification: []
    human_judgment: true
    rationale: Cross-farm behavior needs an authorized runtime review; no tests were added or run.
  - id: D3
    description: .NET solution compiles after adding farm and parcel management.
    verification:
      - kind: other
        ref: "dotnet build src/AgroPlan.sln --no-restore"
        status: pass
    human_judgment: true
    rationale: Compilation does not verify runtime authorization or migration behavior.
metrics:
  duration: 28m
  completed: 2026-11-10
  status: complete
  commits: 2
  plan_head_before: 2e8b40be010c799f9426a353f2ff23387511e48f
  plan_head_after: 8c22fae
---

# Phase 1 Plan 3: Farm Management Summary

**Farm administration and parcel APIs use database-backed membership checks and tenant-scoped resource queries.**

## Performance

- **Duration:** 28m
- **Started:** 2026-11-10T03:47:00Z
- **Completed:** 2026-11-10T04:15:00Z
- **Tasks:** 2
- **Files modified:** 16

## Accomplishments

- Added an `IFarmAuthorization` abstraction backed by active PostgreSQL membership and wired the authorization handler to re-read the current role for each farm-scoped request.
- Added owner-only grant/revoke endpoints for farm administrators; each operation commits a membership change and an append-only audit record containing actor, target, action, and timestamp.
- Added farm listing, update, and delete routes plus parcel create/read/list/update/delete operations. Parcel queries filter by both `farmId` and `parcelId`; the schema enforces a farm relationship and tenant-scoped key.

## Task Commits

1. **Task 1: Harden membership and add administrator access management** — `2b82695`
2. **Task 2: Add tenant-safe farm and parcel operations** — `8c22fae`

## Decisions Made

- Administrator membership can only be granted or revoked by an active farm owner. Farm administrators can manage farm data but cannot grant memberships.
- Membership audit rows intentionally do not cascade with farm or user deletion so the recorded membership changes remain available for audit.
- Parcel area is stored as hectares with positive bounded precision and a farm-scoped uniqueness constraint for parcel names.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added a membership audit store and farm-role authorization abstraction**
- **Found during:** Task 1
- **Issue:** The existing model had no way to record who activated or revoked administrator access, and the authorization handler directly queried the infrastructure context.
- **Fix:** Added `FarmMembershipAuditEntry`, its configuration/migration, and `IFarmAuthorization` with an Infrastructure implementation.
- **Files modified:** `src/AgroPlan.Application/Abstractions/IFarmAuthorization.cs`, `src/AgroPlan.Domain/Farms/FarmMembershipAuditEntry.cs`, `src/AgroPlan.Infrastructure/Persistence/FarmAuthorizationService.cs`, membership API and persistence files.
- **Verification:** .NET 10 solution build passed.
- **Committed in:** `2b82695`

**2. [Rule 2 - Missing Critical] Added a tenant-scoped alternate key for parcels**
- **Found during:** Task 2
- **Issue:** A globally unique parcel id alone does not provide a composite key target for future farm-owned records.
- **Fix:** Added `(FarmId, Id)` as an alternate key and scoped every parcel lookup under the parent farm route.
- **Files modified:** `src/AgroPlan.Domain/Farms/Parcel.cs`, parcel configuration, migration, and controller.
- **Verification:** .NET 10 solution build passed.
- **Committed in:** `2b82695`, `8c22fae`

## Issues Encountered

- No PostgreSQL instance was configured. Migrations were authored but not applied; database transition behavior was not exercised.
- No tests were added or run. Manual acceptance scenarios remain for later authorized verification.

## User Setup Required

Apply migrations in an authorized non-production database before using the new endpoints. No cloud resource or service was provisioned in this plan.

## Next Phase Readiness

Plan 01-04 can now create crop cycles against parcels using their tenant-scoped farm identifiers.

## Self-Check: PASSED

- All four primary controller and migration artifacts exist.
- Task commits `2b82695` and `8c22fae` are ancestors of the current execution branch.
- Both planned tasks were committed; the .NET 10 compile-only verification passed.
