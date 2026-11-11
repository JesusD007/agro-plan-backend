# Phase 4: Applied Farm Intelligence - Context

**Gathered:** 2026-11-11
**Status:** Ready for AI design and planning

<domain>
## Phase Boundary

Deliver the three Phase 4 backend capabilities: voice-based editable activity drafts, farm-week summaries, and photo-based symptom reports. AI may transcribe or describe evidence, but confirmed farm records remain under user control, numeric totals come from structured records, and agronomic diagnoses or treatments require an authorized agronomist's review. This phase does not include visual client UI or new capabilities assigned to later roadmap phases.

</domain>

<decisions>
## Implementation Decisions

### Voice activity drafts
- **D-01:** Support Dominican Spanish and Haitian Creole in the first voice-draft release.
- **D-02:** Accept recordings up to five minutes. When transcription is uncertain, create a partial editable draft and mark uncertain or missing fields for correction rather than silently filling them.
- **D-03:** Workers may create voice drafts only for tasks assigned to them; retain existing task and farm authorization checks.
- **D-04:** A draft never creates a logbook record until the authorized user explicitly confirms it. Delete audio at confirmation or seven days after processing if unreviewed, as required by REQ-rf-g-39.

### Weekly summaries
- **D-05:** Define a week as Monday through Sunday using the farm's configured time zone. Generate a summary only when requested by an owner/administrator, not as a scheduled automatic report.
- **D-06:** A summary covers the whole farm and can be filtered to a crop cycle. Include confirmed records, pending tasks, and missing data. Totals are calculated deterministically from structured records and remain traceable to their sources; AI may only draft explanatory prose.
- **D-07:** Only the farm owner or administrator may review and share a summary. Sharing requires an active advisor grant whose scope permits the data. Advisors can see the summary and listed source records only when those sources are included in their current grant.

### Photo symptom reports
- **D-08:** Before agronomist review, AI may describe only visible signs and request additional photos; it must not give a diagnosis or treatment.
- **D-09:** Only a verified agronomist explicitly assigned or authorized for the farm may confirm diagnosis or treatment. If evidence is insufficient, the agronomist requests more photos/data and the report remains pending.
- **D-10:** Delete report photos when the review is closed or rejected, while retaining the report itself.

### Prior decisions that remain binding
- AI outputs remain drafts and cannot confirm or mutate business records.
- Keep AI integrations provider-neutral; Gemini remains the selected current provider behind the existing `IAiProposalProvider` abstraction.
- Agronomic parameters and numeric rules require validated sources. Do not invent crop facts, quantitative recommendations, or safe-use values.
- Check farm membership in PostgreSQL for every farm-scoped request. Advisor access remains read-only, scoped, revocable, and checked on every request.
- PostgreSQL remains authoritative; keep the existing API/Worker, RabbitMQ outbox, and SignalR job-status patterns.

### the agent's Discretion
- Select implementation details for language identification and transcription, structured output validation, retry/retention jobs, and AI evaluation, provided the decisions above and the provider-neutral boundary are honored.
- Determine the smallest privacy-preserving context needed by each AI operation; never send another farm's data or unnecessary personal identifiers.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and acceptance
- `.planning/ROADMAP.md` — Phase 4 goal and success criteria.
- `.planning/REQUIREMENTS.md` — REQ-rf-g-39, REQ-rf-g-40, and REQ-rf-g-43.
- `../agro-plan-doc/Funcional/AgroPlan - Documentacion Funcional.md` §6.7 — voice logbook, weekly summary, and symptom-photo requirements.
- `../agro-plan-doc/Funcional/AgroPlan - Flujos de Datos por Actor y Función.md` §A3–A4 — voice and weekly-summary workflows, including advisor sharing.
- `../agro-plan-doc/Funcional/AgroPlan - Flujos de Datos y Modelo de Datos.md` §2 — voice processing flow and asynchronous draft lifecycle.
- `../agro-plan-doc/Back/AgroPlan - Arquitectura Backend.md` — API/Worker, storage, AI, messaging, and application architecture.

### Prior locked decisions
- `.planning/phases/01-farm-planning-pilot/01-CONTEXT.md` — provider-neutral AI, explicit confirmation, farm authorization, and supported-data constraints.
- `.planning/phases/02-farm-operations-and-safety/02-CONTEXT.md` — immutable records, advisor grant scope, and deterministic safety data.
- `.planning/phases/02-farm-operations-and-safety/02-AI-SPEC.md` — AI boundaries, provider isolation, privacy, and evaluation practices from Phase 2.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IAiProposalProvider` is the provider-neutral contract currently implemented by Gemini in the Worker.
- `IFileStorage`, private Supabase storage, and logbook attachment endpoints establish file upload/finalization patterns.
- `LogbookEntry` is append-only; `AppendLogbookEntry` creates confirmed records and corrections are linked rather than overwriting originals.
- `OperationsReportReader` calculates cost and production totals from farm-filtered structured data and retains source IDs.
- `AdvisorReportsController` and advisor grants provide farm-scoped, read-only report access.

### Established Patterns
- API authorization re-reads active farm membership from PostgreSQL for each request.
- Long-running AI operations use persisted state, Worker consumers, RabbitMQ outbox events, idempotent processing, and SignalR status notifications.
- AI provider errors, unsupported data, and missing agronomic sources produce explicit unavailable/partial states rather than fabricated records or values.
- PostgreSQL integration tests use Testcontainers; unit tests use xUnit v3; Scalar/OpenAPI coverage exists for API contracts.

### Integration Points
- Voice draft upload and confirmation connect to logbook entries, private file storage, Worker jobs, and job-status notifications.
- Weekly summary queries reuse farm-scoped operational records, work tasks, deterministic report projections, and advisor report scopes.
- Symptom reports connect private image storage, provider-neutral image analysis, farm assignment/authorization, agronomist review, and deletion of source images when the review closes.

</code_context>

<specifics>
## Specific Ideas

- Voice input should support both Dominican Spanish and Haitian Creole.
- Partial or unclear recordings should still give the user an editable draft with uncertain fields called out.
- Summaries should be requested by an authorized farm manager, use the farm's local week, and support whole-farm and crop-cycle views.
- A symptom report must remain pending until an authorized agronomist has enough evidence and explicitly reviews it.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within Phase 4 scope.

</deferred>

---

*Phase: 4-Applied Farm Intelligence*
*Context gathered: 2026-11-11*
