# Phase 1: Farm Planning Pilot - Pattern Map

**Mapped:** 2026-11-09  
**Files analyzed:** 25 likely application/configuration files  
**Analogs found:** 0 / 25

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/AgroPlan.sln` | configuration/build entry | build graph | None | no analog |
| `src/Directory.Build.props` | configuration | build-time | None | no analog |
| `src/AgroPlan.Api/Program.cs` | host/configuration | request-response, hosting | None | no analog |
| `src/AgroPlan.Api/Identity/IdentityEndpoints.cs` | endpoint/controller | request-response | None | no analog |
| `src/AgroPlan.Api/Authorization/FarmMembershipAuthorizationHandler.cs` | authorization handler | request-response | None | no analog |
| `src/AgroPlan.Api/Controllers/FarmsController.cs` | controller | CRUD/request-response | None | no analog |
| `src/AgroPlan.Api/Controllers/ParcelsController.cs` | controller | CRUD/request-response | None | no analog |
| `src/AgroPlan.Api/Controllers/CropCyclesController.cs` | controller | CRUD/request-response | None | no analog |
| `src/AgroPlan.Api/Controllers/LogbookController.cs` | controller | CRUD/request-response, file-I/O | None | no analog |
| `src/AgroPlan.Api/Controllers/PlanningJobsController.cs` | controller | request-response, command/job submission | None | no analog |
| `src/AgroPlan.Api/Hubs/JobStatusHub.cs` | hub/middleware | streaming/event notification | None | no analog |
| `src/AgroPlan.Application/Abstractions/IFarmAuthorization.cs` | service contract | request-response | None | no analog |
| `src/AgroPlan.Application/Abstractions/IClimateProvider.cs` | provider contract | request-response | None | no analog |
| `src/AgroPlan.Application/Abstractions/IGeminiProposalProvider.cs` | provider contract | request-response | None | no analog |
| `src/AgroPlan.Application/Abstractions/IFileStorage.cs` | provider contract | file-I/O | None | no analog |
| `src/AgroPlan.Application/UseCases/SubmitSowingPlan.cs` | use case/service | request-response, event-driven job creation | None | no analog |
| `src/AgroPlan.Application/UseCases/SubmitPlantingFrameProposal.cs` | use case/service | request-response, event-driven job creation | None | no analog |
| `src/AgroPlan.Application/UseCases/ConfirmProposal.cs` | use case/service | request-response/state transition | None | no analog |
| `src/AgroPlan.Domain/Crops/CropCatalogEntry.cs` | model/entity | CRUD/reference data | None | no analog |
| `src/AgroPlan.Domain/Crops/AgronomicParameter.cs` | model/value object | validation/reference data | None | no analog |
| `src/AgroPlan.Domain/Crops/PlantingFrameCalculator.cs` | domain service/utility | deterministic transform | None | no analog |
| `src/AgroPlan.Domain/Logbook/LogbookEntry.cs` | model/entity | append-only write/read | None | no analog |
| `src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs` | data access/configuration | CRUD/transaction | None | no analog |
| `src/AgroPlan.Infrastructure/Outbox/OutboxPublisher.cs` | hosted service | event-driven/pub-sub | None | no analog |
| `src/AgroPlan.Infrastructure/AI/GeminiProposalProvider.cs` | external provider adapter | request-response | None | no analog |
| `src/AgroPlan.Infrastructure/Climate/ClimateProviderAdapter.cs` | external provider adapter | request-response | None | no analog |
| `src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs` | external provider adapter | file-I/O | None | no analog |
| `src/AgroPlan.Worker/Program.cs` | host/configuration | event-driven/hosting | None | no analog |
| `src/AgroPlan.Worker/ProposalJobConsumer.cs` | consumer/worker | event-driven | None | no analog |
| `deploy/api.Dockerfile` | deployment configuration | build/deploy | None | no analog |
| `deploy/worker.Dockerfile` | deployment configuration | build/deploy | None | no analog |
| `deploy/render.yaml` | deployment configuration | service configuration | None | no analog |

These are likely paths inferred from the phase context and research, not existing files. The planner should adjust the exact file set and names as it decomposes the end-to-end slice. The research proposes the API/Application/Domain/Infrastructure/Worker split; keep project boundaries small and avoid adding abstractions without a concrete use.

## Pattern Assignments

There are no tracked application source files in this repository, so there are no code excerpts or in-repository analogs to copy. The only current untracked implementation-adjacent directories are the user's local `.agents/` and `.codex/` GSD installations; they are tooling, not application patterns. No `AGENTS.md` or application-specific project skills were found.

### API endpoints and request authorization

**Analog:** None — greenfield.

**Use the research/context pattern:** ASP.NET Core endpoint composition with dependency injection and structured request validation. Authenticate accounts with ASP.NET Core Identity. For every farm resource, resolve the owning farm and check the current active membership against PostgreSQL at the request boundary; do not trust farm-role claims as the authorization decision. Keep owner and farm-administrator behavior in Phase 1. Apply the same check to reads, writes, proposal submission/confirmation, signed-upload issuance, and SignalR group access.

### Domain models and agronomic rules

**Analog:** None — greenfield.

**Use the research/context pattern:** Keep crop rules and planting-frame capacity calculations deterministic and independent of Gemini. Model parameter provenance, validation state, region/variety applicability, and validity dates. Seed the five selected crop records without invented numerical agronomy. When a required value is absent or unvalidated, return a structured missing-input result instead of a precise numeric recommendation.

### Sowing-plan and planting-frame jobs

**Analog:** None — greenfield.

**Use the research/context pattern:** The API validates and stores an authorized job plus an outbox record in the same PostgreSQL transaction. A separate Worker consumes a stable job identifier from RabbitMQ, reloads only the authorized job's minimal input snapshot, and calls a provider adapter. Persist provider results as a draft with source/config metadata. Require a separate, authorized confirmation command before domain records change. Keep SignalR transient; clients refetch durable job state from the API.

### Gemini and climate adapters

**Analog:** None — greenfield.

**Use the research/AI-SPEC pattern:** Call Gemini through the official `Google.GenAI` SDK from the .NET Worker behind an application-owned interface. Keep the model configurable and the API key in server-side deployment secrets. Constrain output to a DTO/schema, then validate business meaning and provenance in application code. Do not give the model database or tool access. Define `IClimateProvider` separately; the provider remains unselected and must be chosen before production. A missing climate source must remain visible and must not be substituted with invented data.

### PostgreSQL, outbox, and RabbitMQ

**Analog:** None — greenfield.

**Use the research pattern:** PostgreSQL is authoritative. Keep business changes and outbox insertion atomic. The publisher uses broker confirms; consumers acknowledge after idempotent state changes. Queue messages carry stable identifiers, not files or unnecessary farm data. Confirm EF Core and Npgsql major-version compatibility before pinning packages. Use the documented `identidad`, `gestion`, and `infraestructura` schema boundaries where they fit, while ensuring relationships cannot cross farms.

### Supabase Storage and logbook corrections

**Analog:** None — greenfield.

**Use the research/context pattern:** Logbook corrections append a linked operation and preserve the original record. For optional photos, the API checks active farm membership and issues time-limited upload authorization; the client uploads directly to a private Supabase Storage object. Keep metadata and object references in PostgreSQL. Verify the object and farm association before marking it available; never put binary data in queue messages.

### Worker and deployment configuration

**Analog:** None — greenfield.

**Use the research pattern:** Keep API and Worker as separate Docker services with separate startup commands. Reuse the same solution/library boundaries where practical. Configure Supabase, CloudAMQP, Gemini, and climate settings through Render environment secrets; do not commit credentials or put them in client configuration. Use structured .NET console logs with correlation identifiers and redact credentials, audio, and full logbook content.

## Shared Patterns

### Farm-scoped authorization

**Source:** Phase decisions D-01/D-03 and `01-RESEARCH.md` pattern “Farm authorization at command/query boundary.”  
**Apply to:** Every API operation that reads or changes farm-owned data, every job submission/confirmation, every upload authorization, and SignalR group membership.

Resolve resource ownership and active membership together at the server boundary. A valid account or token alone does not grant access to a farm. Recheck membership for long-lived SignalR connections or hub operations so revocation does not rely on stale connection claims.

### Transactional asynchronous work

**Source:** `01-RESEARCH.md` pattern “Transactional outbox with idempotent consumers.”  
**Apply to:** Sowing/planting proposal jobs, status updates, and future asynchronous workflows.

Persist the business/job state and outbox event in one transaction. Publish with confirms, consume idempotently, and keep PostgreSQL state authoritative. Do not publish directly from controllers or depend on SignalR for durable state.

### AI boundary and review

**Source:** `01-AI-SPEC.md` Sections 1, 3–7 and `01-RESEARCH.md` pattern “AI outputs as drafts, deterministic rules as authority.”  
**Apply to:** Gemini calls and any future model-backed workflow.

Treat model output as untrusted structured input. Validate schema, source IDs, missing fields, farm/job association, and deterministic values before saving a draft. Only an authorized explicit user confirmation can produce confirmed farm data. Never include secrets, unrelated farm information, or unnecessary full logbook content in prompts or traces.

### External file transfer

**Source:** Phase decision D-11 and `01-RESEARCH.md` pattern “Direct signed upload.”  
**Apply to:** Optional logbook photo upload.

Keep binary upload off the API and message broker. Issue short-lived authorization after farm checks, store metadata in PostgreSQL, and verify object completion before use.

## No Analog Found

| File/area | Role | Data Flow | Reason |
|-----------|------|-----------|--------|
| All Phase 1 application files above | API, domain, data, worker, provider adapters | CRUD, request-response, file-I/O, event-driven, streaming | Repository has no tracked application code, solution, package manifest, or deployment configuration. |

## Metadata

**Analog search scope:** Repository root and tracked files; `.agents/` and `.codex/` were treated as local GSD tooling, not application source.  
**Files scanned:** No tracked application source files.  
**Pattern extraction date:** 2026-11-09
