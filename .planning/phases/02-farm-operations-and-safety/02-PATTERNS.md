# Phase 2: Farm Operations and Safety — Pattern Map

**Mapped:** 2026-11-10  
**Files analyzed:** current farm-scoped API, domain, persistence, messaging, and test patterns  
**Analogs found:** 6 / 8 areas

> Phase 2 CONTEXT.md and RESEARCH.md had not been written when this map was prepared. The file roles below are candidates inferred from the roadmap requirements; the planner should finalize the exact file list after those artifacts exist.

## File Classification

| Candidate new/modified file | Role | Data flow | Closest tracked analog | Match |
|---|---|---|---|---|
| `src/AgroPlan.Domain/Operations/*` (expenses, labor, input applications, irrigation, harvest) | domain model | CRUD / append-only records | `src/AgroPlan.Domain/Logbook/LogbookEntry.cs` | partial — common farm/parcel/cycle scope and audit fields; structured fields must replace free-form `Details` |
| `src/AgroPlan.Api/Controllers/*OperationsController.cs` | controller | request-response CRUD | `src/AgroPlan.Api/Controllers/LogbookController.cs` | strong — farm-scoped routes, actor extraction, same-farm checks, typed records |
| `src/AgroPlan.Infrastructure/Persistence/Configurations/*OperationConfiguration.cs` | EF mapping | persistence | `src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookEntryConfiguration.cs` | strong — composite farm relationships and farm/time indexes |
| `src/AgroPlan.Infrastructure/Persistence/Migrations/*Operations.cs` | migration | schema/data migration | `src/AgroPlan.Infrastructure/Persistence/Migrations/Logbook.cs` | strong — explicit PostgreSQL DDL in migration `Up`/`Down` |
| `src/AgroPlan.Application/Reports/*` and report API endpoint | query/service/controller | request-response, batch export | `src/AgroPlan.Api/Controllers/CropCyclesController.cs` `List` | partial — farm-filtered read projection exists; no report aggregation/export analog exists |
| `src/AgroPlan.Domain/AdvisoryAccess/*` and access controller | domain/controller | grant/revoke request-response | `src/AgroPlan.Api/Controllers/MembershipsController.cs` | partial — owner-only grant/revoke, transaction, audit; no scoped, time-limited, read-only access model exists |
| advisory/calendar background processing, if required by research | worker/consumer | event-driven | `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs` | strong for durable jobs and atomic claims; only use if the phase requires asynchronous processing |
| Phase 2 tests | integration/unit tests | request-response + persistence | `src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs` and `src/AgroPlan.IntegrationTests/IntegrationSupport.cs` | strong for PostgreSQL/API integration; domain rule unit pattern in `src/AgroPlan.UnitTests/Logbook/LogbookCorrectionTests.cs` |

## Pattern Assignments

### Farm-scoped API CRUD

**Analog:** `src/AgroPlan.Api/Controllers/LogbookController.cs`

- Routes nest resources under `/api/farms/{farmId:guid}/...` and require `FarmMembershipRequirement.PolicyName` (lines 13–15).
- Farm list queries include `Where(x => x.FarmId == farmId)` (lines 21–23).
- Writes extract the actor from `sub`/`NameIdentifier` and check that parcel/cycle foreign keys belong to the same farm (lines 28–41).
- Use `CreatedAtAction` for created records and typed request/response records at the bottom of the controller (lines 43–44, 61–70).

**Alternative CRUD analog:** `src/AgroPlan.Api/Controllers/CropCyclesController.cs`

- It checks `ModelState`, verifies the parcel under the same `farmId`, converts the domain constructor's `ArgumentException` to `BadRequest`, and returns `CreatedAtAction` (lines 49–76).
- Update and read predicates include both the resource ID and `farmId` (lines 35–43, 80–88).

**Authorization service:** `src/AgroPlan.Infrastructure/Persistence/FarmAuthorizationService.cs:9–12` queries the current active membership for each request. Preserve live membership checks rather than trusting a role captured in a token.

### Structured operational records and corrections

**Analog:** `src/AgroPlan.Domain/Logbook/LogbookEntry.cs`

- The entity validates required IDs, actor and details, records occurrence and creation timestamps, and links corrections to an original entry (lines 14–48).
- `src/AgroPlan.Application/UseCases/CorrectLogbookEntry.cs:7–10` creates a new linked row rather than mutating the original.
- `src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookEntryConfiguration.cs:13–27` enforces farm-aware foreign keys to parcels, crop cycles and correction parents.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/Logbook.cs:12–22` creates the PostgreSQL append-only trigger.
- Unit behavior: `src/AgroPlan.UnitTests/Logbook/LogbookCorrectionTests.cs:10–31`. Persistence behavior: `src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs:16–65`.

For Phase 2 operations, preserve the farm/parcel/cycle relationship and actor/time provenance. Do not copy the logbook's free-form `Details` as the data model for money, quantities, product applications, or harvest amounts; those need explicit typed fields and validated units.

### Farm-scoped query/report reads

**Closest analog:** `src/AgroPlan.Api/Controllers/CropCyclesController.cs:26–35` filters by `FarmId`, uses `AsNoTracking`, deterministic ordering, and returns a projection. `src/AgroPlan.Api/Controllers/ParcelsController.cs:17–26` demonstrates selecting typed response records in the EF query.

There is **no existing cost/yield aggregation or file export implementation**. Treat report calculation, unit conversion, CSV/PDF serialization, and download authorization as new patterns; keep report queries farm-filtered and derive totals only from persisted source records. Do not use `LogbookController.List`'s 500-row cap as a reporting strategy.

### Advisor grant/revoke

**Partial analog:** `src/AgroPlan.Api/Controllers/MembershipsController.cs`

- The route is nested under a farm and has the membership authorization policy (lines 14–17).
- Grant/revoke operations check owner role, run in a transaction, and record audit rows (lines 27–30, 36–53, 63–86).

This is **not an exact analog** for Phase 2: `FarmMembership` represents only an active Owner or Administrator and does not model a separate advisor principal, resource scope, expiry, or producer revocation history. Add a distinct grant aggregate and enforce expiry/revocation at request time. Do not add Advisor as a FarmRole without a deliberate design decision.

### PostgreSQL schema changes

**Analog:** `src/AgroPlan.Infrastructure/Persistence/Migrations/Logbook.cs:9–24` defines versioned migrations with explicit SQL and a `Down` migration. Its configuration counterpart is `LogbookEntryConfiguration.cs`.

Use the current migration naming/attributes and `agro` schema conventions. For farm-owned child data, follow the `(FarmId, ResourceId)` alternate-key and composite-FK pattern in `LogbookEntryConfiguration.cs:15–27` and `CropCycleConfiguration.cs:12–30`. Include an index beginning with `FarmId` for common tenant-filtered date/range queries. Any backfill must be explicit, idempotent, and covered by PostgreSQL integration tests.

### Durable async work

**Analog:** `src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs`

- Claims ready rows inside a transaction with `FOR UPDATE SKIP LOCKED` (`:56–69`).
- Uses leases, bounded retries and dead-letter status (`:15–18`, `:91–113`).
- `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs:89–110` validates the event and claims its proposal through an atomic conditional database update before performing work.

Only use this pattern for work that must survive process restarts or needs decoupled delivery (for example, an approved advisory refresh). Simple report queries and synchronous CRUD should remain request-response. Preserve the database as the authoritative record.

### Test fixtures and assertions

**Analog:** `src/AgroPlan.IntegrationTests/IntegrationSupport.cs`

- `ApiTestFactory` hosts the real API and replaces email/storage services with deterministic fakes (`:40–112`).
- `PostgreSqlFixture` starts an isolated PostgreSQL Testcontainer and applies migrations (`:203–237`).
- `TestData.SeedFarmAsync` creates unique farms, owner memberships and parcels (`:328–372`).

Use integration tests for tenant authorization, farm-scoped FKs, migrations, concurrent writes and reports against real PostgreSQL. Follow `CropCycleAndPlanningTests.cs:14–70` for API tests asserting same-farm success and cross-farm rejection. Follow `LogbookAndAttachmentsTests.cs:16–65` for API plus durable-state assertions. Keep deterministic domain calculations (units, safe harvest dates, totals) in unit tests; examples are `LogbookCorrectionTests.cs` and `Planning/PlantingFrameCalculatorTests.cs`.

## Shared Patterns

### Farm scope

**Sources:** `CropCyclesController.cs:13–15`, `FarmAuthorizationService.cs:9–12`, `LogbookEntryConfiguration.cs:21–27`.

Every farm operation should have a farm route, current membership authorization, a `FarmId` predicate, and database relationships that include the same farm ID. Also validate any supplied parcel/cycle IDs before saving.

### Validation and error responses

**Sources:** `CropCyclesController.cs:49–76`, `ParcelsController.cs:45–72`.

Use DataAnnotations on request records, `ModelState` validation, domain constructor validation, 400 for invalid input/foreign association, 404 for a missing farm-scoped resource, 409 for unique/related-record conflicts, and typed response records. Avoid returning EF entities directly.

### Persistence and transaction boundaries

**Sources:** `MembershipsController.cs:36–57`, `OutboxPublisher.cs:55–71`, `CropCycleConfiguration.cs:12–30`.

Use an explicit transaction for multi-row state changes or an outbox write that must commit atomically with its parent. Enforce tenant invariants at both query and schema level.

## No Analog Found

| Phase 2 capability | Why there is no direct analog |
|---|---|
| Cost/yield report aggregation and export | No report query, unit-safe aggregation, or downloadable artifact exists. |
| Scoped, expiring advisor grants | Existing memberships are owner/admin roles without separate scopes or expirations. |
| Product-label withholding intervals and safe-harvest calculation | No verified product catalog, application model, or withholding-period source exists. Treat missing/unknown values as unknown; never invent a duration. |
| Crop calendar rescheduling from confirmed operations and climate alerts | No calendar/task schedule domain exists; the climate adapter currently reports unavailable. |

## Metadata

**Analog search scope:** `src/AgroPlan.Api/Controllers`, `src/AgroPlan.Application/UseCases`, `src/AgroPlan.Domain`, `src/AgroPlan.Infrastructure/Persistence`, `src/AgroPlan.Infrastructure/Messaging`, `src/AgroPlan.Worker/Jobs`, `src/AgroPlan.IntegrationTests`, `src/AgroPlan.UnitTests`.  
**Tracked analogs verified with:** `git ls-files -- <path>`.  
**Pattern extraction date:** 2026-11-10.
