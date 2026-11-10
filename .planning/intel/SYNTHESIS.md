# GSD document synthesis

## Intake

- Documents synthesized: 6 (3 SPEC, 2 PRD, 1 DOC).
- Documents excluded by the user's resolution: `AgroPlan - Flujos de Datos por Actor y Función.md` (the source remains in the documentation repository and may be consulted during later phase planning).
- Locked decisions: 0. No ingested source is an accepted ADR. The backend architecture source is a proposal, not an accepted decision record.
- Requirements extracted: 83 (six confirmed pilot capabilities, RF-G01 through RF-G43, RF-M01 through RF-M28, and RF-T01 through RF-T06).
- Technical constraints: 19 across architecture, protocols, schema, and non-functional rules.
- Context topics: 3.
- Conflicts: 0 blockers, 0 warnings, 4 informational findings.

## User-confirmed project direction

Use the backend architecture proposal as the implementation baseline: ASP.NET Core API and .NET Worker in separate Docker services on Render; Supabase PostgreSQL and Storage; CloudAMQP RabbitMQ; SignalR; and ASP.NET Core Identity. Deliver the confirmed pilot scope first. Keep broader Gestión requirements and Mercado/transport expansions in later phases. This is a user-confirmed session direction, not an accepted ADR in the source documents.

## Scope precedence

The pilot-scope document confirms accounts, farms/parcels, crop/cycle selection, an AI-assisted sowing plan with climate review, AI-assisted planting-frame design, and a manual logbook. The functional specification and unified relational model also describe broader Gestión capabilities and future Mercado/transport scope. Those requirements remain candidates for later phases and are not included in the pilot by default.

## Excluded document

`AgroPlan - Flujos de Datos por Actor y Función.md` was excluded from this ingestion at the user's direction to resolve a reciprocal-reference cycle with `AgroPlan - Flujos de Datos y Modelo de Datos.md`. The latter is synthesized here; its reference to the excluded document is informational and does not block this run. Use the original flow document when planning later workflows where its actor details apply.

## Source limitations

The functional document references `AgroPlan - Version01.docx` and `message.txt`, which were absent from the current source folder at intake. Their contents were not classified or synthesized.

## Artifacts

- Decisions: `.planning/intel/decisions.md`
- Requirements: `.planning/intel/requirements.md`
- Technical constraints: `.planning/intel/constraints.md`
- Context: `.planning/intel/context.md`
- Conflict report: `.planning/INGEST-CONFLICTS.md`
