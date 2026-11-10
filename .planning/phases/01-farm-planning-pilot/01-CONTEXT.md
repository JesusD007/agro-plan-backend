# Phase 1: Farm Planning Pilot - Context

**Gathered:** 2026-11-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver the six confirmed pilot capabilities through the backend: account creation and recovery, farm and parcel management, crop and cycle selection, AI-assisted sowing plans with climate review, AI-assisted planting-frame design, and a manual logbook. Every farm-scoped operation must authorize the active user against that farm. Owners and farm administrators are the active pilot operators; worker and advisor workflows remain in later phases.

</domain>

<decisions>
## Implementation Decisions

### Authentication and farm authorization
- **D-01:** Use ASP.NET Core Identity for accounts. For every farm-scoped request, validate the user's current active farm membership in the database; do not treat farm-role claims in an access token as sufficient authorization.
- **D-02:** Keep the documented support-mediated recovery flow: a code is sent to the account email, support reviews the request, and an approved user receives a temporary password-change mechanism. Support never sees the user's password. Code lifetime, attempt limits, and session revocation policy remain to be specified during planning.
- **D-03:** The pilot activates owner and farm-administrator workflows. Worker and advisor access remain deferred with their roadmap phases.

### AI and climate integration
- **D-04:** Google AI Studio is the selected Gemini development and key-management environment. The .NET Worker calls the Gemini API through the official `Google.GenAI` C# SDK; the key is a server-side secret and is never sent to a client. Keep model selection configurable.
- **D-05:** The climate-data provider is not selected. Define a climate-provider contract now and choose a source before production; do not fabricate weather data when the provider is unavailable.
- **D-06:** AI outputs are reviewable proposals. A sowing plan or planting-frame choice becomes confirmed farm data only after explicit user approval. Agronomic calculations remain deterministic and use validated parameters.

### Crop catalog and agronomic rules
- **D-07:** The initial crop catalog includes plátano, yuca, batata, arroz, and habichuela.
- **D-08:** When a crop lacks validated agronomic parameters, show the missing fields and withhold unsupported numeric recommendations. Do not invent parameters or present model estimates as validated facts.
- **D-09:** Preserve parameter source and validity metadata so agronomists can validate and update crop rules without changing business logic.

### Manual logbook and files
- **D-10:** Treat logbook entries as immutable. A correction is a new operation linked to the entry it corrects, preserving the original and its history.
- **D-11:** Support optional photos for logbook activities using the documented direct-to-Supabase-Storage flow with time-limited authorization issued by the API. Store metadata and references in PostgreSQL; never place binary content in RabbitMQ messages.
- **D-12:** PostgreSQL remains the source of truth. Background work uses the API/Worker, RabbitMQ, an outbox, idempotent processing, and SignalR notifications as specified by the adopted architecture.

### the agent's Discretion
- Choose the bearer-token and refresh-token details that fit ASP.NET Core Identity and the API clients, while keeping farm authorization request-time and server-side.
- Choose the .NET data-access and migration tooling during research and planning, honoring the documented PostgreSQL schemas and cross-farm integrity rules.
- Define bounded timeouts, retry limits, and error mapping for Gemini and climate integrations; retain a visible pending/failed state and never turn external service failure into a confirmed farm record.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Pilot scope and permissions
- `../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md` — six confirmed capabilities, roles, farm-scoped permission matrix, and support-mediated account recovery.

### Backend architecture and data model
- `../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md` — API/Worker boundary, .NET, Supabase, CloudAMQP, SignalR, outbox, file flow, logging, and immutable logbook correction proposal.
- `../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md` — PostgreSQL schemas, farm-owned entities, async jobs, AI drafts, audit trail, and outbox constraints.
- `../agro-plan-doc/Doc/AgroPlan - Diagrama Relacional Unificado.md` — conceptual relationships and entities; defer entities outside the pilot to their mapped phases.

### Requirements and domain evidence
- `../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md` — requirement details including optional logbook photos and later-phase functionality.
- `../agro-plan-doc/Doc/Entrevista.txt` — local crop and production context for the five selected pilot crops.
- `../agro-plan-doc/Doc/AgroPlan - Flujos de Datos por Actor y Función.md` — excluded from initial ingestion by user direction because of reciprocal references; consult when planning worker, advisor, marketplace, or transport phases.

### Gemini integration
- `https://ai.google.dev/gemini-api/docs/libraries` — official Google GenAI SDK language support, including C# `Google.GenAI`.
- `https://ai.google.dev/gemini-api/docs/api-key` — official Gemini API key handling and restrictions.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None. The repository is greenfield and has no application source or package manifests.

### Established Patterns
- None in application code. The user-installed `.agents/` and `.codex/` directories provide local GSD tooling and must not be treated as application modules.

### Integration Points
- Build an ASP.NET Core API and a separate .NET Worker as Docker services.
- Connect the API and Worker to Supabase PostgreSQL/Storage and CloudAMQP; use SignalR for live job updates, with PostgreSQL state authoritative.
- Keep vendor credentials in server-side deployment secrets; the Worker calls Gemini and the climate adapter.

</code_context>

<specifics>
## Specific Ideas

- The success metric for the pilot is that all six confirmed capabilities work end-to-end through the API with farm-scoped permissions applied.
- Include five crops in the starting catalog, but expose missing expert-validated inputs instead of fabricating recommendations.
- Gemini integration should use the official C# SDK and keep its model configurable.

</specifics>

<deferred>
## Deferred Ideas

- Worker invitations, task assignment, offline synchronization, advisors, costs, crop safety and withholding periods, extended reporting, and expanded AI features belong to later Gestão phases.
- Marketplace, buyer/seller transactions, traceability, transport, equipment rental, and client UI work are outside this pilot phase.

</deferred>

---

*Phase: 1-farm-planning-pilot*
*Context gathered: 2026-11-09*
