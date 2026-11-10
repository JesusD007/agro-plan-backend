# Phase 2: Farm Operations and Safety — Research

**Researched:** 2026-11-10  
**Domain:** Farm operations, safety rules, reporting, calendars and delegated access  
**Confidence:** MEDIUM

## Summary

Build Phase 2 on the existing ASP.NET Core/.NET 10, EF Core, Npgsql and PostgreSQL foundation. Keep structured, farm-scoped operational records as the source for reports and safety decisions; use explicit units and provenance, and retain original quantities. The repo already contains working patterns for request-time farm membership authorization, farm-aware composite keys, typed records, migrations and xUnit v3 tests. [VERIFIED: `src/Directory.Build.props:1-8`; `src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj:1-14`; `src/AgroPlan.Api/Controllers/LogbookController.cs:12-70`; `src/AgroPlan.Infrastructure/Persistence/Configurations/CropCycleConfiguration.cs:9-34`; `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj:1-20`]

Treat crop advice, stage calendars, harvest eligibility, cost-per-quintal and reference prices as evidence-dependent outputs. Missing or mismatched label, unit, agronomic parameter, or climate source must remain visibly missing or unknown; do not infer values. Phase 1 decisions explicitly require request-time membership checks, provider-neutral climate contract with source selection before production, and no unsupported agronomic numerical recommendations. [VERIFIED: `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md` — “For every farm-scoped request, validate the user's current active farm membership in the database”; “The climate-data provider is not selected”; “show the missing fields and withhold unsupported numeric recommendations.”]

**Primary recommendation:** Add farm-scoped structured event records and append-only corrections; compute farm/cycle reports from confirmed source rows; model withholding periods from applicable verified product labels; represent calendars, prices and climate advisories with source and validity; authorize advisors through live, scoped, expiring grants.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REQ-rf-g-02 | “Registrar manualmente gastos, jornales y materiales por día y cultivo.” | Store typed dated entries against farm/cycle with original unit and currency. [VERIFIED: `.planning/REQUIREMENTS.md:68-72`] |
| REQ-rf-g-03 | “Calcular en tiempo real el costo de producción por quintal.” | Calculate only when costs, harvest quantity, unit conversion and quintal convention are known; exact rules remain a planning decision. [VERIFIED: `.planning/REQUIREMENTS.md:74-78`] |
| REQ-rf-g-04 | “Proveer calendario estándar de tareas por cultivo.” | Use source-backed, versioned crop task templates; absent validated values remain unavailable. [VERIFIED: `.planning/REQUIREMENTS.md:80-84`] |
| REQ-rf-g-07 | “Bloquear la fecha de cosecha tras registrar químicos hasta cumplir el periodo de carencia.” | Resolve per product/formulation/crop-use label and application; missing evidence cannot clear a hold. [VERIFIED: `.planning/REQUIREMENTS.md:98-102`; Ministry label guidance cited below] |
| REQ-rf-g-08 | “Consultar precios de referencia.” | Preserve channel, crop, market/region, observation date, unit, currency and source; official Ministry page publishes dated wholesale/retail files. [VERIFIED: `.planning/REQUIREMENTS.md:104-108`; [Ministry inter-daily prices](https://agricultura.gob.do/informe-de-precios-interdiario/)] |
| REQ-rf-g-13 | “Generar calendario del ciclo desde preparación hasta cosecha, actualizar avance con labores confirmadas y proponer cambios por ejecución o alertas vigentes de lluvia/viento.” | Derive progress from confirmed operations; schedule proposals require operator confirmation and current sourced alerts. [VERIFIED: `.planning/REQUIREMENTS.md:134-138`] |
| REQ-rf-g-15 | “Exportar reportes de costos y rendimiento.” | Farm-filtered report projections must identify underlying records, units and conversion rules. [VERIFIED: `.planning/REQUIREMENTS.md:146-150`] |
| REQ-rf-g-31 | “Registrar riego con fecha, método (goteo, inundación, lluvia) y fuente de agua.” | Add typed irrigation event and farm/cycle association. [VERIFIED: `.planning/REQUIREMENTS.md:242-246`] |
| REQ-rf-g-32 | “Registrar aplicaciones de plaguicidas, fungicidas, fertilizantes y otros insumos, incluso con dron, y alertar frecuencia y periodo de carencia.” | Persist product, use, application, method, amount/unit and evidence. Assess application frequency against exact validated product/crop/use rules and return an explicit unavailable result when absent/stale; keep frequency alerts distinct from pesticide PHI, MRL and worker re-entry intervals. [VERIFIED: `.planning/REQUIREMENTS.md:248-252`; [Ministry pesticide manual](https://agricultura.gob.do/transparencia/wp-content/uploads/2023/02/Manual-Distribuidores-de-Plaguicidas-2da-Edicion.pdf)] |
| REQ-rf-g-33 | “Ofrecer guía orientativa de fertilización por etapa, recordatorios y aviso de que no sustituye al técnico.” | Only offer stage guidance from validated, dated sources and clearly mark it as indicative. [VERIFIED: `.planning/REQUIREMENTS.md:254-258`] |
| REQ-rf-g-34 | “Reforzar recordatorios en primeras semanas/etapas críticas del cultivo.” | Reminder timing depends on validated stage/crop parameters, otherwise indicate the missing configuration. [VERIFIED: `.planning/REQUIREMENTS.md:260-264`] |
| REQ-rf-g-35 | “Alertar lluvia, viento fuerte, sequía y temporada ciclónica, incluido el evaluador de siembra.” | Climate provider is unselected; carry source, issue time, geographic applicability and validity; stale/unavailable is not a no-risk result. [VERIFIED: `.planning/REQUIREMENTS.md:266-270`; `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md` D-05] |
| REQ-rf-g-36 | “Exportar reporte automático de cultivo, área, fecha de siembra y cantidad cosechada.” | Export only authorized farm/cycle records and retain original area/yield units. [VERIFIED: `.planning/REQUIREMENTS.md:272-276`] |
| REQ-rf-g-37 | “Dar a asesor/agente de área acceso a datos de fincas y reportes con autorización del productor.” | Distinct advisor grant per farm with explicit scope, expiry, revocation and per-request evaluation. [VERIFIED: `.planning/REQUIREMENTS.md:278-282`] |
| REQ-rf-g-42 | “Mostrar ventana estimada de cosecha y lista de acciones según siembra, parámetros y avance; registrar fecha/cantidad real y restricciones de carencia.” | Estimate only with validated parameters; preserve actual harvest data and evaluate all applicable withholding constraints. [VERIFIED: `.planning/REQUIREMENTS.md:308-312`] |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| Expense, labor, material, irrigation, input and harvest records | API / Backend | Database / Storage | API validates actor, farm scope and business rules; PostgreSQL owns durable typed records. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions] |
| Cost/yield projections and exports | API / Backend | Database / Storage | Server-side aggregate queries derive outputs from persisted confirmed rows; database filters by farm and cycle. [CITED: https://learn.microsoft.com/en-us/ef/core/querying/] |
| Withholding eligibility | API / Backend | Database / Storage | Deterministic application rule reads verified application and label evidence before harvest status is returned. [CITED: Ministry manual URL in Phase Requirements] |
| Crop calendars and climate advisories | API / Backend | Database / Storage | API supplies versioned rule/source metadata and validity; PostgreSQL stores confirmed schedule state. Live weather source remains unselected. [VERIFIED: `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md` D-05,D-06,D-09] |
| Advisor delegated access | API / Backend | Database / Storage | Each resource request must resolve current farm grant scope, expiry and revocation before data is returned. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased] |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---|---:|---|---|
| ASP.NET Core | .NET 10 target | Farm-scoped HTTP API and resource authorization | Current repository target; retain established API stack. [VERIFIED: `src/Directory.Build.props:1-8`] |
| Entity Framework Core + Npgsql | Existing references: EF Core 10.0.0 / Npgsql provider 10.0.3 | PostgreSQL persistence, migrations, typed projections | Already used in Infrastructure; do not add a second data access stack. [VERIFIED: `src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj:1-14`] |
| PostgreSQL | project managed instance | Constraints, foreign keys, numeric storage, indexes and durable records | PostgreSQL is existing project source of truth. [VERIFIED: `.planning/ROADMAP.md:5-6`; `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md` D-12] |
| xUnit v3 | 3.2.2 in current test project | Unit and integration tests | Existing unit and integration projects already use xUnit v3. [VERIFIED: `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj:1-20`; `src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj:1-24`] |

### Supporting

| Library / facility | Version | Purpose | When to Use |
|---|---:|---|---|
| PostgreSQL `numeric` | built-in | Exact decimal money/quantity arithmetic | Use for money and measured quantities when decimal precision is part of domain requirements; determine precision/scale with domain owner. [CITED: https://www.postgresql.org/docs/current/datatype-numeric.html] |
| ASP.NET Core `IAuthorizationService` | .NET 10 platform | Resource-based authorization | Use for advisor access after loading farm/scope resource; declarative `[Authorize]` alone cannot decide access based on a loaded resource. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased] |
| EF Core `AsNoTracking` and LINQ projections | EF Core 10 | Read-only report queries | Use for bounded, farm-filtered list/report reads; avoid materializing all records for aggregation. Existing code already uses farm-filtered `AsNoTracking`. [VERIFIED: `src/AgroPlan.Api/Controllers/LogbookController.cs:16-24`] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|---|---|---|
| Manually parsing Ministry price reports as an assumed live API | Import/version official datasets or accept an explicitly sourced manual reference value | Ministry site shows downloadable XLSX/CSV/ODS market reports, but this research did not verify a stable public API; imports need freshness and mapping maintenance. [CITED: https://agricultura.gob.do/informe-de-precios-interdiario/] |
| Treating all operations as free-form logbook details | Typed expense, application, irrigation and harvest records | Structured fields make validation, safety decisions and report aggregation possible; this is consistent with the requirement for source-data-derived totals. [VERIFIED: `.planning/REQUIREMENTS.md` REQ-rf-g-02,03,07,31,32] |

**Installation:** No new packages are recommended for the Phase 2 backend patterns in this research. Reuse project references; confirm any future package need through official documentation and package-legitimacy checks before planning installation.

## Architecture Patterns

### Recommended Project Structure

```text
src/
├── AgroPlan.Domain/Operations/       # typed operational records and deterministic rules
├── AgroPlan.Application/Reports/     # farm/cycle report projections and export models
├── AgroPlan.Api/Controllers/         # farm-scoped request endpoints
├── AgroPlan.Infrastructure/Persistence/Configurations/ # EF mapping and PostgreSQL migrations
└── AgroPlan.UnitTests/ + AgroPlan.IntegrationTests/    # rules and API/database boundaries
```

### Pattern 1: Farm-scoped structured records

**What:** Use farm-nested routes, current membership authorization, `FarmId` filters, typed response records, same-farm relationship checks and composite tenant-aware foreign keys. Existing `LogbookController` and `CropCycleConfiguration` demonstrate these conventions. [VERIFIED: `src/AgroPlan.Api/Controllers/LogbookController.cs:12-70`; `src/AgroPlan.Infrastructure/Persistence/Configurations/CropCycleConfiguration.cs:9-34`]

**When to use:** For expense, labor/material, irrigation, input application, harvest, calendar and report endpoints.

**Example:**

```csharp
var rows = await db.HarvestRecords.AsNoTracking()
    .Where(x => x.FarmId == farmId && x.CropCycleId == cycleId)
    .OrderBy(x => x.HarvestedAt)
    .Select(x => new HarvestReportRow(x.HarvestedAt, x.Quantity, x.UnitCode))
    .ToListAsync(ct);
```

`HarvestRecords`, `HarvestedAt`, `Quantity`, and `UnitCode` are illustrative names, not existing repository values. [ASSUMED]

### Pattern 2: Evidence-backed safety and calculations

Represent each input application with date/time, product identity, crop/use, method, amount/unit and label evidence; keep the source/validity of any derived withholding rule. Compute earliest eligible harvest only from a verified applicable label and all relevant application records. Unknown identity, crop match or interval produces unknown/review status, never zero days. [CITED: https://agricultura.gob.do/transparencia/wp-content/uploads/2023/02/Manual-Distribuidores-de-Plaguicidas-2da-Edicion.pdf; `.planning/phases/02-farm-operations-and-safety/02-DOMAIN-RESEARCH.md`]

Store observed values with the original unit and compute aggregates only after validated conversion rules; don't sum incompatible units or currencies. Repository domain intel says quintal equivalence, precision, rounding and unit catalog are pending. [VERIFIED: `.planning/intel/constraints.md` — “La equivalencia del quintal, precisión, redondeo y reglas de ajuste siguen pendientes.”]

### Pattern 3: Resource-based advisor authorization

Create a separate grant resource with farm ID, advisor identity, scopes, granted-by, purpose, start/end and revoked-at audit data. Load the scoped farm resource and call `IAuthorizationService.AuthorizeAsync` on each request; `[Authorize]` is insufficient for decisions that depend on a loaded resource. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased]

### Pattern 4: Atomic writes; explicit transaction only for multi-step unit

EF Core automatically transacts a single `SaveChanges` when the provider supports transactions. Use an explicit transaction for multiple saves/queries that form one atomic operation, such as a grant plus audit event or a parent write plus related rows. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---|---|---|---|
| User/farm permission | Custom role claim parsing or cached advisor checks | Existing farm membership authorization plus resource-based `IAuthorizationService` and current grant lookup | A loaded resource's authorization requires an imperative check; Phase 1 locks request-time DB membership. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased; VERIFIED: `01-CONTEXT.md` D-01] |
| Decimal financial arithmetic | `double` or binary floating point totals | PostgreSQL `numeric` and .NET decimal with explicit precision/scale | PostgreSQL numeric is exact decimal arithmetic; select scales based on agreed data rules. [CITED: https://www.postgresql.org/docs/current/datatype-numeric.html] |
| Weather values | Synthetic values or stale cached forecast presented as current | Provider-neutral climate contract and explicit unavailable/stale status until source and freshness are approved | Phase 1 explicitly leaves climate source unselected and prohibits fabricated weather. [VERIFIED: `01-CONTEXT.md` D-05] |
| Crop advice and PHI rules | Generic numbers or AI-generated values | Versioned source-backed parameters / registered product-label evidence; deterministic rule evaluation | Phase 1 withholds unsupported numeric recommendations; product-label applicability is safety-critical. [VERIFIED: `01-CONTEXT.md` D-08,D-09; CITED: Ministry manual URL above] |

**Key insight:** A result is trustworthy only when its source records, unit meanings, applicability and validity can be reconstructed; AI can explain a deterministic result but cannot establish the underlying agronomic or chemical facts. [VERIFIED: `01-CONTEXT.md` D-06,D-08,D-09]

## Common Pitfalls

1. **Clearing harvest from incomplete chemical data.** A missing PHI must not become zero; verify exact product/formulation, crop-use and label version, and retain an unknown/review outcome. [CITED: Ministry pesticide manual URL above; `.planning/phases/02-farm-operations-and-safety/02-DOMAIN-RESEARCH.md`]
2. **Plausible but wrong unit totals.** Preserve recorded unit, crop context and source; aggregate after a validated compatible conversion only. Quintal equivalence, precision and rounding are unresolved. [VERIFIED: `.planning/intel/constraints.md` quoted above]
3. **Confusing reference price with realized farm revenue.** Ministry inter-daily report lists distinct wholesale and retail datasets; display channel, market, date, unit and source, and do not call a stale observation a current offer. [CITED: https://agricultura.gob.do/informe-de-precios-interdiario/]
4. **Invented crop-stage schedule.** No numeric crop stage/parameter can be inferred from catalog identity alone; display missing configuration until a qualified source is validated. [VERIFIED: `01-CONTEXT.md` D-07–D-09]
5. **Expired or wrong-area weather alert.** Return its source, affected area and validity; expired/unavailable data is not evidence of no risk. Climate provider remains unselected. [VERIFIED: `01-CONTEXT.md` D-05]
6. **Advisor access survives revocation.** Evaluate grant scope, expiration and revocation against current persisted state on each resource request; avoid relying on long-lived claims. [CITED: resource authorization docs above; VERIFIED: `01-CONTEXT.md` D-01]

## Code Examples

### Farm-scoped read projection (existing pattern)

```csharp
var rows = await db.LogbookEntries.AsNoTracking()
    .Where(x => x.FarmId == farmId)
    .OrderByDescending(x => x.OccurredAt)
    .Take(500)
    .ToListAsync(ct);
```

This exact pattern is already present in `LogbookController`; report endpoints should add cycle/date filters and project only fields required for the report. [VERIFIED: `src/AgroPlan.Api/Controllers/LogbookController.cs:16-19`]

### Resource authorization (framework pattern)

```csharp
var authorization = await authorizationService.AuthorizeAsync(
    User, farmScopedGrant, AdvisorReadRequirement.PolicyName);
if (!authorization.Succeeded) return Forbid();
```

Resource names are illustrative. The official ASP.NET Core guidance requires imperative resource authorization after the resource is available. [CITED: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased]

## State of the Art

| Older / unsafe approach | Recommended approach | Impact |
|---|---|---|
| Trust farm or advisor role embedded in access token for ongoing authorization | Re-read current membership/grant and authorize the loaded resource | Revocation can take effect on the next request. [VERIFIED: `01-CONTEXT.md` D-01; CITED: resource-based auth docs] |
| Sum source rows by presumed universal unit | Preserve original quantities; normalize only through validated, source-backed conversion | Prevents mismatched-unit cost/yield results. [VERIFIED: `.planning/intel/constraints.md` quoted above] |
| Treat a single `SaveChanges` as requiring a manually managed transaction | Let EF Core use its default atomic `SaveChanges`; explicitly transact multi-step business operations | Avoids unnecessary transaction management while preserving atomicity. [CITED: https://learn.microsoft.com/en-us/ef/core/saving/transactions] |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|---|---|---|
| A1 | Phase 2 records can use append-only correction semantics as the default for material changes. | Architecture Patterns | Some editable draft fields may need ordinary updates; distinguish drafts from confirmed records during discussion. |
| A2 | Advisor grants should default to read-only least-privilege scopes. | Architecture Patterns | Product may require specific write actions; broad defaults risk exposing data or enabling unapproved changes. |
| A3 | API-level CSV export is sufficient for first report delivery unless another format is specified. | Standard Stack | Producers/advisors may require XLSX/PDF or client-side rendering. |

## Open Questions

1. **(RESOLVED)** Operational records are immutable once confirmed; corrections are new linked entries retaining actor, time, reason, and prior values, per D-06. Drafts that have not become confirmed operational records may be corrected before confirmation.
2. **(RESOLVED)** No currency is defaulted. Every monetary value retains its currency; totals in unlike currencies stay separate unless an explicit conversion includes rate, source, and effective date, per D-08. Use decimal storage and defer presentation rounding to the documented output precision; never round source values before aggregation.
3. **(RESOLVED)** Production gate: no exact crop-specific quintal convention, conversion source, or shared-cost allocation rule is approved. Preserve source units, show no cost-per-quintal metric where conversion is absent, and keep shared expenses itemized until an expert-validated conversion/allocation rule is configured, per D-07 and D-09.
4. **(RESOLVED)** Production gate: unknown or unverified PHI blocks harvest registration/eligibility; there is no producer override, and MRL/re-entry values cannot substitute, per D-01 and D-02. A label may enter validated status only through a recorded review using the exact official product/formulation, crop/use, jurisdiction, version, source, and effective dates; define the authorized reviewer/administrative workflow before enabling production label ingestion.
5. **(RESOLVED)** The producer grants read-only access to an explicit allowlist of farm/report categories, including financial categories only when explicitly selected. A current farm Owner/Administrator grants or revokes; grant scope and expiry are checked on every request, per D-04 and D-05.
6. **(RESOLVED)** Production gate: no climate source or freshness threshold has been selected. Until source, geographic applicability, issue/validity fields, and freshness policy are approved, advisories return unavailable/stale with no inference of no risk, per D-10.
7. **(RESOLVED)** Production gate: the Ministry dataset is a possible source, but no stable API or automated feed is verified. Keep reference prices unavailable until an authorized import/manual-entry source, market/channel mapping, provenance, and freshness policy are selected; do not represent reference prices as realized sale prices, per D-09 and D-10. [CITED: https://agricultura.gob.do/informe-de-precios-interdiario/]
8. **(RESOLVED)** Use CSV for report exports, generated on request and streamed without retaining report artifacts; preserve source-row traceability, original units, and currency grouping, per the agent's discretion under D-07 through D-09.

REQ-rf-g-32 frequency alert resolution: expose a deterministic application-frequency assessment with `compliant`, `approaching`, `exceeded`, or `unavailable` status only when a validated, exact product/crop/use rule is configured. Missing or stale rules return `unavailable` with the missing-evidence reason; do not infer a limit. Add unit/API tests for both configured and unavailable cases.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|---|---|---|---|---|
| .NET SDK | Build/test | ✗ | SDK list only shows 8.0.204 | Install/use .NET 10 SDK in execution environment. [VERIFIED: command `dotnet --list-sdks`] |
| .NET runtime | API/test execution | ✓ | 10.0.11 | Use deployment image matching `net10.0`. [VERIFIED: command `dotnet --list-runtimes`] |
| Docker CLI | Testcontainers PostgreSQL and local services | ✓, daemon unavailable/unconfirmed | CLI 28.4.0; `docker info` produced no server version | Provision a running Docker daemon or use isolated non-production PostgreSQL. [VERIFIED: `docker --version`; `docker info --format ...` returned empty] |
| PostgreSQL | Persistence/integration tests | External service | — | Existing integration tests use PostgreSQL Testcontainers; execution requires reachable Docker or test DB. [VERIFIED: `src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj:10-15`] |
| Climate source | Current weather advisories | Not selected | — | Expose explicit unavailable status until selected and validated. [VERIFIED: `01-CONTEXT.md` D-05] |
| Ministry price source | Reference prices | Official downloadable files visible | Dated XLSX/CSV/ODS; no API contract verified | Manually ingest sourced dataset or keep prices unavailable until import source is selected. [CITED: https://agricultura.gob.do/informe-de-precios-interdiario/] |

**Missing dependencies with no fallback:** None for designing Phase 2. A .NET 10 SDK and an available PostgreSQL test environment are required before build/integration verification.

## Validation Architecture

Nyquist validation is treated as enabled because `.planning/config.json` is absent. [VERIFIED: file check — `.planning/config.json` does not exist]

### Test Framework

| Property | Value |
|---|---|
| Framework | xUnit v3 3.2.2; Microsoft.NET.Test.Sdk 18.11.0. [VERIFIED: `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj:1-20`] |
| Config file | Existing unit/integration test project configuration and `xunit.runner.json`. [VERIFIED: `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj:15-20`] |
| Quick run command | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` |
| Full suite command | `dotnet test src/AgroPlan.sln`. [VERIFIED: `src/AgroPlan.sln` exists] |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|---|---|---|---|---|
| REQ-rf-g-02/31/32/42 | Typed record validation, same-farm references and data persistence | unit + PostgreSQL/API integration | `dotnet test` targeted unit/integration projects | New tests required |
| REQ-rf-g-03/15/36 | Totals and exports preserve unit/source and farm scope | unit + PostgreSQL/API integration | same | New tests required |
| REQ-rf-g-07 | Verified PHI calculation, multiple applications, unknown/mismatched evidence and early harvest behavior | unit + PostgreSQL/API integration | same | New tests required |
| REQ-rf-g-04/13/33/34 | Calendar source validity, progress update from confirmed work, operator confirmation and missing-rule behavior | unit + PostgreSQL/API integration | same | New tests required |
| REQ-rf-g-08/35 | Price/weather source, validity and stale/unavailable status | unit + integration | same | New tests required |
| REQ-rf-g-37 | Farm/scope/expiry/revocation checked per request; cross-farm and unauthorized export denied | API/security integration | same | New tests required |

### Wave 0 Gaps

- [ ] Add test fixtures for verified product-label/PHI cases and explicit unknown/mismatch states.
- [ ] Add unit definitions and representative conversions only after expert-approved sources exist; test that incompatible units are not aggregated.
- [ ] Add API integration fixtures for two farms, advisor grants with distinct scopes, expiry and immediate revocation.
- [ ] Add report export tests proving farm scope and source record traceability.
- [ ] Ensure .NET 10 SDK and Docker/PostgreSQL environment are available before running the suite.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---|---|---|
| V2 Authentication | Yes | Existing ASP.NET Core Identity and authenticated farm requests. [VERIFIED: `src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj:7-9`] |
| V3 Session Management | Yes | Revalidate member/advisor grant from persisted state per request; revocation cannot depend on token expiry. [VERIFIED: `01-CONTEXT.md` D-01; CITED: resource auth docs] |
| V4 Access Control | Yes | Farm-scoped filters, composite tenant-aware foreign keys, explicit grant resource scope and owner-controlled revocation. [VERIFIED: `CropCycleConfiguration.cs:12-34`; CITED: resource auth docs] |
| V5 Input Validation | Yes | Validate amount/unit/date/product, crop-cycle association and export filters server-side; reject invalid cross-farm IDs. [VERIFIED: existing `LogbookController.cs:28-43`] |
| V6 Cryptography | Yes | Use platform Identity/token primitives; do not implement cryptography in new operation logic. [VERIFIED: Identity package reference above] |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---|---|---|
| Cross-farm object reference in operation/report/export | Elevation of privilege / Information disclosure | Farm-scoped query and composite FK; integration test another farm's IDs. [VERIFIED: `CropCycleConfiguration.cs:21-28`] |
| Advisor sees out-of-scope or revoked report | Information disclosure | Per-request resource authorization against persisted grant scope/status/expiry; do not authorize from stale claims. [CITED: resource auth docs; VERIFIED: `01-CONTEXT.md` D-01] |
| Unsupported PHI claims mark harvest safe | Tampering / Safety harm | Require exact label applicability and provenance; unknown remains unknown/review, with deterministic rules and audit trail. [CITED: Ministry pesticide manual URL above] |
| Report leaks financial or personal records through broad export | Information disclosure | Apply the same farm and field/scope authorization to export query as to API reads; output only granted categories. [VERIFIED: `.planning/REQUIREMENTS.md` REQ-rf-g-37] |

## Project Constraints (from AGENTS.md)

No `AGENTS.md` was present in the backend working directory. [VERIFIED: project instruction-file discovery]

## Sources

### Primary / authoritative

- Microsoft Learn, resource-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased
- Microsoft Learn, EF Core transactions: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- Microsoft Learn, EF Core saving and query tracking: https://learn.microsoft.com/en-us/ef/core/saving/
- PostgreSQL documentation, numeric types: https://www.postgresql.org/docs/current/datatype-numeric.html
- PostgreSQL documentation, constraints: https://www.postgresql.org/docs/current/ddl-constraints.html
- Dominican Ministry of Agriculture, pesticide distributor manual: https://agricultura.gob.do/transparencia/wp-content/uploads/2023/02/Manual-Distribuidores-de-Plaguicidas-2da-Edicion.pdf
- Dominican Ministry of Agriculture, inter-daily market prices: https://agricultura.gob.do/informe-de-precios-interdiario/
- Project sources: `.planning/ROADMAP.md`, `.planning/REQUIREMENTS.md`, `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md`, `.planning/intel/constraints.md`, `.planning/phases/02-farm-operations-and-safety/02-DOMAIN-RESEARCH.md`, `.planning/phases/02-farm-operations-and-safety/02-PATTERNS.md`.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — directly verified in existing project files; no new package is proposed.
- Architecture: MEDIUM — farm authorization/persistence analogs are verified; reports, advisor grants and crop safety have no direct implemented analog.
- Pitfalls: MEDIUM — official framework and PostgreSQL references support implementation patterns; domain-specific thresholds and units remain unresolved.

**Research date:** 2026-11-10  
**Valid until:** 2026-12-10 for architecture; recheck product-label data, climate source, Ministry price feed and package versions before implementation/production.
