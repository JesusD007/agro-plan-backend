# AgroPlan Backend

## What This Is

AgroPlan Backend provides the API and background services for agricultural producers to plan and record farm operations. It starts with the confirmed pilot workflows and grows into workforce operations, production management, marketplace, and transport capabilities.

## Core Value

Producers can rely on secure, farm-scoped operational records and reviewable assistance from planting through harvest.

## Business Context

- **Customer**: Agricultural producers and farm administrators in the Dominican Republic.
- **Success metric**: All six confirmed pilot capabilities work end-to-end through the API with farm-scoped permissions applied.

## Requirements

### Validated

(None yet — the pilot has not shipped.)

### Active

- Deliver the six confirmed pilot capabilities: accounts, farms/parcels, crop/cycle selection, AI-assisted sowing planning with climate review, AI-assisted planting-frame design, and a manual logbook.
- Deliver the remaining documented Gestión capabilities and later Mercado and transport components in the roadmap phases after the pilot.

### Out of Scope

- Frontend and mobile UI implementation — this repository is the backend; client repositories consume its APIs and events.
- Automatic acceptance of AI output — users review and confirm generated drafts and plans.
- Marketplace, transport, equipment rental, advanced worker operations, and other expanded capabilities in the pilot — these remain in later roadmap phases.

## Context

- The backend repository is greenfield.
- The user selected the later backend architecture proposal as the implementation baseline: ASP.NET Core API and .NET Worker in separate Docker services on Render, Supabase PostgreSQL and Storage, CloudAMQP RabbitMQ, SignalR, and ASP.NET Core Identity.
- PostgreSQL is the source of truth. Farm-scoped authorization is enforced by the server. The API and Worker use an outbox and idempotent event handling for asynchronous work.
- AI provider, climate data provider, agronomic rules, and several operational policies remain to be selected during phase planning.
- The document `AgroPlan - Flujos de Datos por Actor y Función.md` was excluded from initial ingestion to resolve a reciprocal-reference cycle. Its source remains in the documentation repository and must be consulted when planning relevant later phases.
- The functional document referenced `AgroPlan - Version01.docx` and `message.txt`, which were absent during intake.

## Constraints

- **Tech stack**: ASP.NET Core and .NET Worker — selected by the user from the later backend architecture proposal.
- **Persistence and files**: Supabase PostgreSQL is the source of truth; Supabase Storage holds binary files.
- **Async processing**: CloudAMQP RabbitMQ carries jobs/events; SignalR notifies connected clients. PostgreSQL state remains authoritative.
- **Security**: Every farm operation is authorized by user, farm, role, and resource. Advisor access requires scoped, time-bound producer consent.
- **AI**: Generated plans, drafts, and summaries remain reviewable; deterministic agronomic calculations are not delegated to AI.
- **Scope**: The six confirmed pilot capabilities precede the expanded functional specification.
- **Privacy**: Operational logs must not contain credentials, tokens, identity documents, audio, or full logbook content.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Use the later ASP.NET Core, Supabase, CloudAMQP, and SignalR architecture proposal | It is the backend-specific architecture selected by the user for this implementation | — Pending |
| Deliver the confirmed pilot before the broader product portfolio | The pilot-scope document is narrower than the functional specification | — Pending |
| Keep PostgreSQL as the source of truth and require farm-scoped authorization | These are core security and consistency constraints in the architecture proposal | — Pending |

---
*Last updated: 2026-11-09 after document ingestion and roadmap initialization*
