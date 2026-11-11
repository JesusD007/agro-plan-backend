# Phase 4: Applied Farm Intelligence - Pattern Map

**Mapped:** 2026-11-11  
**Files analyzed:** 15 candidate files/areas (voice drafts/jobs/media lifecycle, weekly projection/share, symptom report/review, persistence, tests)  
**Analogs found:** 13 / 15 (mostly partial; no exact Phase 4 workflow exists)

## File Classification

| New/Modified File or area | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/AgroPlan.Api/Controllers/VoiceDraftsController.cs` | controller | request-response, file-I/O | `Controllers/PlanningJobsController.cs`, `Controllers/TaskEvidenceController.cs` | role-match |
| `src/AgroPlan.Application/Abstractions/ISpeechTranscriptionProvider.cs` | provider | request-response | `Abstractions/IAiProposalProvider.cs` | role-match |
| `src/AgroPlan.Worker/Jobs/VoiceDraftConsumer.cs` | worker | event-driven | `Worker/Jobs/SowingPlanConsumer.cs` | strong role-match |
| voice draft/job entities, EF mappings, migration | model, config, migration | CRUD, event-driven | attachment and advisor grant mappings | role-match |
| `src/AgroPlan.Worker/Jobs/ExpiredVoiceMediaCleanup...` | worker | batch, file-I/O | `IFileStorage` / `SupabaseFileStorage` plus worker jobs | partial |
| `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs` | controller | request-response, batch | `OperationsReportsController.cs`, `AdvisorReportsController.cs` | role-match |
| weekly summary projection/service | service | transform, request-response | `Application/Reports/OperationsReportReader.cs` | strong role-match |
| advisor summary/source access | authorization/controller | request-response | `AdvisorReportsController.cs`, `AdvisorAccessController.cs` | strong role-match |
| `src/AgroPlan.Api/Controllers/SymptomReportsController.cs` | controller | request-response, file-I/O | `LogbookAttachmentsController.cs`, `TaskEvidenceController.cs` | role-match |
| symptom image description provider/worker | provider, worker | file-I/O, event-driven | `IAiProposalProvider.cs`, `SowingPlanConsumer.cs` | partial |
| agronomist authorization and review service/controller | authorization, service, controller | request-response | `TaskReviewsController.cs`, `AdvisorAccessController.cs` | partial; no agronomist gate analog |
| private media deletion lifecycle | service, worker | file-I/O, batch | `IFileStorage.cs`, `SupabaseFileStorage.cs` | role-match |
| EF entity configurations and feature migrations | config, migration | CRUD | `LogbookAttachmentConfiguration.cs`, `AdvisorAccessGrantConfiguration.cs` | role-match |
| unit and PostgreSQL/RabbitMQ tests | test | transform, request-response, event-driven | `OperationsReportTests.cs`, `LogbookAndAttachmentsTests.cs`, `OutboxAndConsumerTests.cs` | strong role-match |

## Pattern Assignments

### Voice draft controller and confirmation

**Analogs:** `src/AgroPlan.Api/Controllers/PlanningJobsController.cs` and `TaskEvidenceController.cs` (tracked).

`PlanningJobsController` lines 14-22 establishes `[ApiController]`, a farm-scoped route, membership policy, and constructor DI. Lines 24-66 validate identity and farm-scoped inputs, persist a job/proposal and outbox message in one transaction, then return `AcceptedAtAction`. Lines 68-93 show farm/job scoping, role re-check, explicit ready-state conflict, and user confirmation. `TaskEvidenceController` lines 14-41 demonstrates that a worker operation must additionally verify active Worker membership, task assignment and task state; lines 43-74 persist an idempotent client operation ID with the upload intent.

```csharp
[ApiController]
[Route("api/farms/{farmId:guid}/planning-jobs")]
[Authorize(Policy = FarmMembershipRequirement.PolicyName)]
public sealed class PlanningJobsController(...) : ControllerBase
```

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
db.SowingPlanProposals.Add(proposal);
db.OutboxMessages.Add(outboxMessage);
await db.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
return AcceptedAtAction(nameof(Get), new { farmId, jobId = proposal.Id }, ...);
```

For D-01..D-04, keep the voice provider port neutral (no Gemini-specific interface name), make the language activation gate explicit, cap media at five minutes, persist partial transcription and uncertainty/missing-field markers, and make confirmation an explicit authorized transition that creates the logbook record. Worker assignment checks from `TaskEvidenceController` apply to worker-originated drafts. Persist job/media state and outbox before enqueue; cleanup eligibility must cover confirmation and seven days after processing. There is no existing audio deletion scheduler, so use the worker/job hosting pattern and `IFileStorage` delete capability rather than implying an existing exact analog.

### Provider-neutral AI and asynchronous jobs

**Analogs:** `src/AgroPlan.Application/Abstractions/IAiProposalProvider.cs`, `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs` (tracked).

`IAiProposalProvider.cs` lines 3-55 keeps input/output records and failure kinds in Application and exposes a cancellation-aware interface. `SowingPlanConsumer.cs` lines 17-22 injects the application port and logger into a `BackgroundService`; lines 41-74 set up RabbitMQ consumption. Its later validation and typed provider failure handling (around lines 209-249) show that provider output must be validated and transient/configuration/generation failures classified. Implement speech and image-description ports using this shape; do not treat Gemini proposal support as speech or Haitian Creole approval.

### Private media intake and finalization

**Analogs:** `src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs` lines 13-37, 40-79, 81-125; `TaskEvidenceController.cs` lines 14-98; `Application/Abstractions/IFileStorage.cs`; `Infrastructure/Storage/SupabaseFileStorage.cs`.

The attachment endpoint checks MIME type and size, checks parent record farm ownership, creates a short-lived upload authorization, persists media metadata, and returns a signed URL. Finalization uses an idempotent state claim, checks expiry and stored metadata against expected type/size, finalizes the object, and releases the claim. Task evidence provides the worker assignment-specific equivalent and operation idempotency. `IFileStorage` is the storage boundary; `SupabaseFileStorage` uses private object keys, signed URLs, staged-to-final copy, and delete. Reuse these privacy and verification controls for voice and symptom photos. Phase 4 must define safe image/audio allowlists and limits instead of blindly copying the current logbook image limits. Delete media at the D-04/D-11 lifecycle point; retain report/transcript metadata required by the decisions.

### Farm-local weekly summary projection and advisor scope

**Analogs:** `src/AgroPlan.Application/Reports/OperationsReportReader.cs`, `OperationsReportsController.cs`, `AdvisorReportsController.cs`, `AdvisorAccessController.cs` (tracked).

`OperationsReportReader` is a pure deterministic projection over structured records and includes source IDs/missing-input markers; preserve that boundary for weekly totals. `AdvisorReportsController.cs` lines 13-19 establishes authorized farm-nested routes; lines 21-29 validates dates and cycle belongs to farm, queries with `AsNoTracking`, delegates calculation to the reader, and returns its projection. Its private `Allowed` helper at lines 83-88 authorizes a resource/scope through ASP.NET authorization. `AdvisorAccessController` lines 15-19 uses membership policy and grant service for grant lifecycle. Use these patterns to query confirmed entries and pending work, derive Monday-Sunday boundaries from farm timezone (not server local time), filter by farm/cycle, and produce stable source references. Sharing must re-evaluate the active advisor grant and its compatible scope at read time; constrain sources individually as well as summary access. AI can produce explanatory prose only after receiving this bounded projection; compute numbers in application code.

```csharp
if (!await Allowed(farmId, AdvisorReportScope.Costs)) return Forbid();
if (cropCycleId.HasValue && !await db.CropCycles.AsNoTracking()
    .AnyAsync(x => x.FarmId == farmId && x.Id == cropCycleId, ct)) return NotFound();
var expenses = await db.ExpenseEntries.AsNoTracking()
    .Where(x => x.FarmId == farmId).ToListAsync(ct);
return Ok(reader.ReadCosts(farmId, cropCycleId, from, to, expenses, harvests));
```

### Symptom report and agronomist review workflow

**Analogs:** `LogbookAttachmentsController.cs`, `TaskReviewsController.cs`, `AdvisorAccessController.cs`.

`TaskReviewsController.cs` lines 16-35 performs role/identity and input/reason validation; lines 32-74 locks the row, checks state, appends a decision-history event, and persists state plus outbox atomically. Adapt the append-only review/state-transition pattern to symptom reports: AI result remains visible-sign observations and photo/data requests only; uncertain evidence remains pending, and only a verified agronomist with explicit/assigned farm authorization may confirm. `AdvisorAccessController` is the closest dynamic grant lifecycle, but its authorization scope is advisor-report access, not agronomist credentials. No exact agronomist verification/authorization gate exists; define an explicit policy/resource requirement and test negative cases. On close/reject, remove images from private storage while retaining the report and decision history (D-08..D-11).

### Persistence and migrations

**Analogs:** `src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookAttachmentConfiguration.cs` lines 7-23 and `AdvisorAccessGrantConfiguration.cs` lines 8-22 (tracked).

These map feature-owned tables under `agro`, define primary and farm-scoped alternate keys, field limits/required fields, unique object-key indexes, farm-scoped indexes, and composite foreign keys with restrictive deletion. Use equivalent tenant-safe keys for media/report/job entities; add indexes for pending cleanup/review/job states and explicit retention timestamps. Keep migrations feature-oriented, consistent with the accepted GSD override that migrations are separate by feature.

### Tests

**Analogs:** `src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs`; `src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs`, `AdvisorAccessTests.cs`, `OperationsReportsTests.cs`, `Worker/OutboxAndConsumerTests.cs` (all tracked).

`OperationsReportTests` is the pure deterministic projection test analog. PostgreSQL API integration tests use `PostgreSqlFixture`, `ApiTestFactory`, authenticated owner/advisor clients, and seeded relational state; the report test at `OperationsReportsTests.cs` lines 14-40 demonstrates cycle/farm setup and immutable correction history. `OutboxAndConsumerTests` is the async worker/outbox analog. Build tests across the existing test layers: provider contract validation and partial transcription; five-minute boundary and provider activation gate; assigned-task/farm authorization and explicit confirmation; idempotent upload/cleanup; timezone boundaries, deterministic totals/source IDs/missing data; share scope and revocation; photo privacy/deletion; verified agronomist-only review and insufficient-evidence pending state; outbox retry and per-job failure isolation.

## Shared Patterns

### Authorization

**Sources:** `TaskEvidenceController.cs` (worker-assignment authorization), `FarmAuthorizationService.cs` (active membership every request), `AdvisorReportsController.cs` (resource/scope policy).

Use authenticated farm-nested routes. Re-check active membership/grant on each request. Workers must be assigned to the relevant task. Advisor summary/source access must require a currently active, compatible grant. Agronomist verification is a new policy requirement; no existing policy establishes it.

### Transactions, outbox, and state changes

**Sources:** `PlanningJobsController.cs` lines 24-66; `TaskReviewsController.cs` lines 32-74.

Save the job or review transition and outbox event in the same PostgreSQL transaction. Use explicit allowed-state checks and append-only decision history for confirmations/corrections. Worker consumers should be idempotent and classify retryable vs terminal provider/storage failures.

### Privacy and storage

**Sources:** `LogbookAttachmentsController.cs`; `IFileStorage.cs`; `SupabaseFileStorage.cs`.

Store private media under farm-scoped object keys; issue only short-lived signed access; verify metadata at finalization; keep metadata rows linked to farm and parent record. Model deletion as a stateful worker operation so retries do not orphan state. Apply phase-specific audio/photo retention instead of copying attachment lifetime defaults.

### Deterministic reports

**Sources:** `OperationsReportReader.cs`; `AdvisorReportsController.cs`.

Keep projection/calculation pure and separately unit-tested. API owns authorization, farm/cycle/date validation and reads. Totals are traceable to persisted source IDs and preserve original units unless a validated conversion exists. Model prose cannot change projection values.

## No Analog Found

| File/area | Role | Data Flow | Reason |
|---|---|---|---|
| Neutral speech transcription contract with Dominican Spanish + Haitian Creole approval gate | provider | request-response | Existing `IAiProposalProvider` is for agronomic sowing proposals; no speech provider or validated Creole path exists. |
| Verified agronomist account and explicit per-farm symptom-review authorization | authorization/model | request-response | Existing grant policies cover advisors/reports and farm roles, not agronomist verification. |
| Farm-local Monday-Sunday period boundary helper | utility | transform | Existing reports support filters but no farm-timezone weekly period contract was found. |
| Audio expiration and symptom-photo close/reject cleanup worker | worker | batch, file-I/O | Storage supports object deletion, but no matching retention scheduler/state machine exists. |

## Metadata

**Analog search scope:** `src/AgroPlan.Api/Controllers`, `src/AgroPlan.Application/{Abstractions,Reports}`, `src/AgroPlan.Infrastructure/{Persistence,Storage}`, `src/AgroPlan.Worker/Jobs`, `src/AgroPlan.UnitTests`, `src/AgroPlan.IntegrationTests`.  
**Tracked-source gate:** all named analogs verified with `git ls-files`.  
**Pattern extraction date:** 2026-11-11
