# Phase 2: Farm Operations and Safety - Context

**Gathered:** 2026-11-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver farm-scoped operational records and evidence-based safety/reporting through the backend: expenses, labor and material usage, irrigation, input applications, harvest records, cost/yield and production reports, crop calendars, price/weather advisories, and producer-authorized advisor access. PostgreSQL remains the system of record. Every operation checks current farm membership; advisor requests also check the current grant scope, expiry, and revocation.

Offline synchronization and worker task management are Phase 3. Voice and weekly AI summaries, image-based agronomy, marketplace/transport, and client UI are outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Harvest safety and agronomic evidence
- **D-01:** Do not mark a harvest eligible when an applicable application lacks a validated withholding period for the exact registered product, crop, and use. Unknown evidence blocks harvest registration until reviewed; missing does not mean zero days.
- **D-02:** Keep withholding calculations deterministic and traceable to the application, approved label evidence, source version, and computed earliest safe date. Maximum-residue limits, worker re-entry intervals, and pre-harvest intervals are distinct and cannot substitute for one another.
- **D-03:** No unsupported crop-stage dates, fertilizer rates, climate thresholds, or agronomic values. Return missing/unavailable status until validated evidence exists.

### Advisor access and privacy
- **D-04:** Advisor grants are read-only and limited by farm and explicit data/report scope, with bounded validity and producer-controlled revocation. Check grant state and scope on every request for immediate revocation.
- **D-05:** Keep advisor grants separate from Owner/Administrator membership. Do not grant account-wide access or edits through advisor grants.

### Operational records and reporting
- **D-06:** Expenses, applications, irrigation, harvest, and other confirmed operational records are immutable. A correction is a new linked record preserving actor, time, reason, and prior values.
- **D-07:** Preserve entered quantities and original units. Convert only through explicit, source-backed rules and show conversion/source with calculated metrics. Do not aggregate incompatible units.
- **D-08:** Store a currency code on every monetary amount. Do not silently convert currencies or combine unlike currencies; a conversion must include rate, source, and effective date.
- **D-09:** Derive cost, yield, and production reports from structured source records, scoped to farm/cycle and traceable to source rows. A reference price is not a realized sale price or guaranteed quote.

### External data and safe unavailable states
- **D-10:** No climate provider or price feed is selected. Include source, market/channel/geography, unit/currency, observation or issue time, and validity/freshness; return unavailable/stale when no current source is configured. No data is not no risk or a zero price.
- **D-11:** Crop calendars, stage reminders, fertilizer guidance, and harvest windows include source/validation state and effective dates. Missing rules mean missing configuration, not an invented schedule.
- **D-12:** Keep the API/Worker/PostgreSQL baseline and per-request membership validation from Phase 1. Use async processing only for durable external refresh/scheduling; keep ordinary writes and bounded reports synchronous.

### the agent's Discretion
- Choose schemas, endpoint grouping, correction links, numeric precision, report query implementation, and export format (CSV is a reasonable first API format) while preserving provenance and unit/currency integrity.
- Research official/manual price and climate ingestion approaches; leave feeds unconfigured until source and freshness policy are approved.
- No currency default was selected. Keep currency per amount and totals separate unless an explicit conversion rate/source/date is supplied.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Scope and requirements
- ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md §6.1, §6.2, §6.6, §6.7 — operational, safety, calendar, reports, advisor, and harvest requirements.
- .planning/REQUIREMENTS.md — Phase 2 acceptance statements.
- .planning/ROADMAP.md — Phase 2 boundary and success criteria.

### Actor flows and data model
- ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos por Actor y Función.md §A2, §A4, §A7, §D1 — operations, confirmed records, scoped advisor reports, and price references.
- ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md — PostgreSQL/API/Worker data-flow constraints.
- ../agro-plan-doc/Doc/AgroPlan - Diagrama Relacional Unificado.md — conceptual farm-owned relationships.
- ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md — API/Worker, PostgreSQL, RabbitMQ, SignalR, and outbox.

### Prior decisions and research
- .planning/phases/01-farm-planning-pilot/01-CONTEXT.md — authorization, safe-data, and provider decisions.
- .planning/phases/01-farm-planning-pilot/SECURITY.md — open security gates relevant to safety and external services.
- .planning/phases/02-farm-operations-and-safety/02-DOMAIN-RESEARCH.md — PHI, units, weather, price, and advisor criteria.
- .planning/phases/02-farm-operations-and-safety/02-RESEARCH.md — technical research and validation map.
- .planning/phases/02-farm-operations-and-safety/02-PATTERNS.md — existing backend analogs.
- .planning/intel/constraints.md — validated-data and tenant-scope constraints.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- FarmAuthorizationService rechecks active Owner/Administrator membership in PostgreSQL per request.
- Farm-nested controllers, same-farm checks, composite tenant-aware foreign keys, EF configurations, migrations, xUnit v3, and PostgreSQL Testcontainers are established patterns.
- Immutable logbook entries, linked corrections, database triggers, and unit/integration tests model audit-sensitive records.
- API test host and PostgreSQL fixture can be reused for farm-scoped endpoints and advisor grants.

### Established Patterns
- Use typed request/response records, farm-scoped queries, and same-farm reference validation; do not return EF entities directly.
- Persist actor/timestamps and use explicit transactions for multi-row operations.
- Use the outbox/Worker only for durable or decoupled processing.
- There is no existing cost/yield aggregation, export, advisor grant, crop calendar, or validated withholding implementation. These are new modules, not extensions to free-form logbook details.

### Integration Points
- Operation endpoints and report projections connect API/Application/Domain to PostgreSQL.
- Advisor reports require resource-based authorization against a persisted grant after loading the requested resource.
- Price and climate advisories need configured sources and expiry; climate remains unselected and safe-mode behavior must remain available.
- Harvest safety consumes structured applications and applicable label data; it must not depend on Gemini or unverified AI output.

</code_context>

<specifics>
## Specific Ideas

- Missing product/crop-use label, application date, or approved withholding period blocks harvest eligibility.
- Advisor grants remain read-only, farm-scoped, category-scoped, time-limited, and immediately revocable.
- Preserve original units; every conversion names a validated rule and source.
- Reference prices show channel, crop/variety, region/market, unit, currency, observation date, and source.

</specifics>

<deferred>
## Deferred Ideas

- Offline synchronization and worker task assignment/verification are Phase 3.
- Voice drafts, weekly AI summaries, and image-based symptom review are Phase 4.
- Marketplace, transport, equipment rental, and client UI are later phases.

</deferred>

---
*Phase: 2-farm-operations-and-safety*
*Context gathered: 2026-11-10*

