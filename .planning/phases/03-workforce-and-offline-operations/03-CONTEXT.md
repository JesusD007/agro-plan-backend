# Phase 3: Workforce and Offline Operations - Context

**Gathered:** 2026-11-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver the backend capabilities for farm administrators and workers to coordinate field tasks, evidence, verification, task-linked wages/rewards, routes, and safe synchronization after reconnection. The phase is farm-scoped and builds on the existing ASP.NET Core API/.NET Worker, PostgreSQL, RabbitMQ/outbox, SignalR, and storage patterns. The client UI remains outside this repository. Geographic provider activation is gated until a provider is approved.

</domain>

<decisions>
## Implementation Decisions

### Worker invitations and farm access
- **D-01:** Workers join through a single-use email link. Pending invitations expire after seven days, can be renewed or cancelled by a farm administrator, and count against the basic-plan limit of five workers.
- **D-02:** Preserve per-request farm authorization and re-read current membership from PostgreSQL so revoked access takes effect immediately. Worker access is limited to assigned work and permitted history; do not expose farm financial records.

### Tasks, evidence, verification, and wages
- **D-03:** Photo evidence is required per task according to its configured rule. Location and time collection are optional and require worker consent.
- **D-04:** A requested correction creates a new linked submission revision; keep the prior submission and evidence intact.
- **D-05:** The administrator sets wage amount and unit at task assignment. The amount is confirmed only after the task is verified. AgroPlan records wages but does not execute payments.

### Offline synchronization and conflicts
- **D-06:** Each offline operation carries a stable client-generated unique ID reused on retries for idempotency.
- **D-07:** Return a result for each operation in a sync batch. Continue independent operations when one fails and identify retryable failures.
- **D-08:** Preserve both task versions when a stale client update conflicts with server state. A farm administrator explicitly resolves the conflict; never silently overwrite either version.

### Points, badges, rankings, and rewards
- **D-09:** Extra points use objective criteria configured per task; do not automatically score photo quality.
- **D-10:** Rankings are disabled by default and can be enabled by a farm administrator.
- **D-11:** Badges use configured rules. Rewards are manually granted by the administrator; no payment processing is included.

### Routes and offline route data
- **D-12:** Define a provider-neutral geographic/routing contract. Keep route generation unavailable until a provider is selected and approved.
- **D-13:** Offline route data includes ordered task stops and available route geometry, without offline base maps or turn-by-turn navigation.
- **D-14:** If assigned tasks or parcels change after confirmation, mark the route stale. The administrator reviews and confirms a new route version.

### Calendar updates and partial failures
- **D-15:** A scheduling change creates a new version, retains the previous assignment in audit history, and notifies the worker.
- **D-16:** If an external calendar update fails after AgroPlan saves the task, keep the task, expose synchronization as pending, and retry it.
- **D-17:** Batch processing isolates failures, returns per-operation outcomes, and continues independent work.

### the agent's Discretion
- Choose entity boundaries, endpoint grouping, sync response codes, retention policy details, and notification mechanisms consistent with existing API/outbox patterns and the decisions above.
- Define explicit limits and validation for invitation tokens and evidence uploads from existing security and storage constraints.
- Propose the geographic-provider selection gate and operational criteria; do not activate a provider without approval.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase boundary and requirements
- `.planning/ROADMAP.md` — Phase 3 goal, requirements, and success criteria.
- `.planning/REQUIREMENTS.md` — acceptance statements for `REQ-rf-g-06,09,12,14,17,18,19,20,21,22,23,25,26,27,28,29,30,41`.
- `.planning/PROJECT.md` — architecture baseline and farm-scope/security constraints.

### Functional scope and actor flows
- `../agro-plan-doc/Funcional/AgroPlan - Documentacion Funcional.md` §§4.1, 6.1, 6.4–6.6, 8.3, 14 — roles, plan limits, worker experience, gamification, field-derived requirements, notifications, and offline/nonfunctional constraints.
- `../agro-plan-doc/Funcional/AgroPlan - Flujos de Datos por Actor y Función.md` §§A2, A5, A6 — offline synchronization, worker task/evidence/review/gamification, and route flows. This is the actor-flow source intentionally excluded during initial document ingestion.
- `../agro-plan-doc/Funcional/AgroPlan - Flujos de Datos y Modelo de Datos.md` — system-of-record and data-flow model.

### Prior decisions and implementation patterns
- `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md` — authentication, farm membership, immutable corrections, outbox, and upload security decisions.
- `.planning/phases/02-farm-operations-and-safety/02-CONTEXT.md` — current authorization, persistence, and audit patterns.
- `.planning/phases/02-farm-operations-and-safety/02-PATTERNS.md` — reusable implementation patterns and integration points.
- `.planning/phases/02-farm-operations-and-safety/02-SECURITY.md` — security controls and unresolved threats relevant to worker access and evidence.
- `.planning/intel/constraints.md` — tenant isolation, source-of-truth, and evidence constraints.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FarmAuthorizationService` validates current active farm membership from PostgreSQL on each farm-scoped request; extend role/scope checks without trusting stale JWT membership claims.
- Existing email sender, identity/session, outbox, background worker, SignalR, private file-upload, immutable logbook, and linked-correction patterns can support invitations, task notifications, evidence, and sync jobs.
- Existing PostgreSQL Testcontainers fixtures and xUnit v3 suites provide patterns for tenant-isolation, concurrency, persistence, and API contract coverage.

### Established Patterns
- Use DTOs and farm-scoped queries; validate cross-entity farm ownership and persist actor/timestamps.
- PostgreSQL is authoritative. Durable async side effects use outbox/worker processing and idempotent handling.
- Audit-sensitive corrections append linked records instead of editing prior confirmed evidence.
- Existing authentication and membership checks are per request; worker/administrator access should follow that model.

### Integration Points
- New worker/task modules connect API, Application, Domain, Infrastructure, and PostgreSQL, with worker notification and email side effects dispatched durably.
- Task submissions and evidence use private storage and must be bound to farm/task/submission authorization.
- Offline sync needs transactional idempotency keys, per-operation outcomes, and explicit persisted conflict resolution.
- Calendar effects and route updates should be tied to confirmed farm/task changes while retaining source versions and retry state.

</code_context>

<specifics>
## Specific Ideas

- Worker invitations use a seven-day single-use email link; pending invitations count toward the basic tier's limit of five workers.
- Wages are configured at assignment and become confirmed only on verification; AgroPlan does not pay them.
- Preserve every submitted evidence revision and every conflicting task version.
- Offline routes carry ordered stops and any available geometry, not maps or turn-by-turn directions.
- No geographic provider is approved yet; expose a neutral contract and safe unavailable state.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within Phase 3 scope.

</deferred>

---

*Phase: 3-workforce-and-offline-operations*
*Context gathered: 2026-11-10*
