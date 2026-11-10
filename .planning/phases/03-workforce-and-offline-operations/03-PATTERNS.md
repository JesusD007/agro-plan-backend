# Phase 3: Workforce and Offline Operations - Pattern Map

**Mapped:** 2026-11-10  
**Files analyzed:** 18 representative source/test files  
**Analogs found:** 14 / 18 capability groups; worker/task/sync/route domains have no direct analog.

## File Classification

This is a role-level map for the phase scope. The planner should make the concrete file list after reading `03-RESEARCH.md`.

| New/Modified File Group | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| Worker invitations and token acceptance | API controller, application service, domain model, EF config/migration | request-response + email | `src/AgroPlan.Api/Identity/IdentityEndpoints.cs`, `src/AgroPlan.Infrastructure/Identity/RefreshSessionService.cs`, `src/AgroPlan.Infrastructure/Email/ConfiguredEmailSender.cs` | role-match |
| Farm role/scope authorization for workers | authorization handler/service | request-response | `src/AgroPlan.Api/Authorization/FarmMembershipAuthorizationHandler.cs`, `src/AgroPlan.Infrastructure/Persistence/FarmAuthorizationService.cs` | partial |
| Tasks, assignments, reviews, wages, points, badges, rewards | API controllers, application use cases, domain models, EF configs/migrations | CRUD + state transitions | `src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs`, `src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs` | role-match |
| Task submissions and evidence revisions | controller, application use case, domain model, storage adapter | request-response + file I/O | `src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs`, `src/AgroPlan.Domain/Logbook/LogbookEntry.cs`, `src/AgroPlan.Application/UseCases/CorrectLogbookEntry.cs` | role-match |
| Offline synchronization operation ledger and conflicts | controller, application handler, domain model, EF config/migration | batch + transactional CRUD | `src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs`, `src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs`, `src/AgroPlan.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs` | partial |
| Durable task/calendar notifications and retries | event model, outbox persistence, publisher/consumer, worker job | event-driven + batch | `src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs`, `src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs` | role-match |
| Provider-neutral route contract and unavailable implementation | application abstraction, domain value objects, infrastructure provider | request-response + batch | `src/AgroPlan.Application/Abstractions/IClimateProvider.cs`, `src/AgroPlan.Worker/Climate/UnconfiguredClimateProvider.cs` | role-match |
| Unit tests for task, invitation, sync and route rules | unit test | in-process state transitions | `src/AgroPlan.UnitTests/Logbook/LogbookCorrectionTests.cs`, `src/AgroPlan.UnitTests/Calendars/CropCalendarEvaluatorTests.cs` | role-match |
| API and PostgreSQL integration tests | integration test | HTTP + database + broker | `src/AgroPlan.IntegrationTests/Api/IdentityAndFarmAuthorizationTests.cs`, `src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs`, `src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs` | role-match |

All paths above were confirmed as git-tracked source.

## Pattern Assignments

### Farm-scoped API and authorization

**Analogs:** `src/AgroPlan.Api/Authorization/FarmMembershipAuthorizationHandler.cs` (lines 17–32), `src/AgroPlan.Infrastructure/Persistence/FarmAuthorizationService.cs` (lines 7–13), and `src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs` (lines 18–20, 77–95).

The authorization handler extracts the user ID and `farmId` from the authenticated request, then calls the application abstraction backed by a fresh PostgreSQL query. The service selects only active membership and the caller's role. Existing endpoints add a narrower role check after loading the resource when the policy only grants Owner/Administrator.

```csharp
dbContext.FarmMemberships.AsNoTracking()
    .Where(membership => membership.FarmId == farmId && membership.UserId == userId && membership.IsActive)
    .Select(membership => (FarmRole?)membership.Role)
    .SingleOrDefaultAsync(cancellationToken);
```

**Apply to:** invite administration, worker acceptance, task assignment and review, reward administration, worker history, route access, sync, and conflict resolution. Worker access requires an explicit role/resource check beyond the current Owner/Administrator policy. Resolve a task under the route's farm before exposing or mutating it; do not authorize from token claims alone.

### Email invitation and single-use credentials

**Analogs:** `src/AgroPlan.Api/Identity/IdentityEndpoints.cs` (lines 11–28, 100–111), `src/AgroPlan.Application/Abstractions/IEmailSender.cs` (lines 1–6), `src/AgroPlan.Infrastructure/Email/ConfiguredEmailSender.cs` (lines 11–42), and `src/AgroPlan.Infrastructure/Identity/RefreshSessionService.cs` (lines 100–117).

Identity endpoints use explicit request records, validation, rate limiting, and small HTTP results. `IEmailSender` keeps SMTP implementation in Infrastructure. `RefreshSessionService` demonstrates generating high-entropy token bytes and persisting only a SHA-256 token hash.

```csharp
internal static string HashToken(string token) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

private static string CreateToken()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    return WebEncoders.Base64UrlEncode(bytes);
}
```

**Apply to:** store only a hash of the single-use invite secret, bind the invitation to farm/email/role, enforce expiry/revocation/consumption atomically, and send via `IEmailSender`. Avoid placing invite secrets in logs or returning a secret after creation. The account recovery flow is not a direct model because its reset is mediated by Support; use the token storage pattern only.

### Append-only evidence and private uploads

**Analogs:** `src/AgroPlan.Domain/Logbook/LogbookEntry.cs` (lines 14–56), `src/AgroPlan.Application/UseCases/CorrectLogbookEntry.cs` (lines 5–11), `src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs` (lines 12–130), and `src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs` (lines 15–68).

The domain entity has private setters and represents a correction as a new row with `CorrectsEntryId` and a required reason. The API checks MIME type and size, persists a pending upload record, uses a short-lived signed upload, verifies uploaded object metadata during finalization, then moves it to an immutable object key. Integration tests verify append-only history and database rejection of row mutation/deletion.

```csharp
new(Guid.NewGuid(), original.FarmId, original.ParcelId, original.CropCycleId,
    original.ActivityType, occurredAt, details, actorId, now, original.Id, reason);
```

**Apply to:** each worker submission/correction is a new revision linked to the previous one. Task evidence should be authorized against farm + task + submission and stored privately. Keep the evidence reference immutable after verification; enforce the per-task photo rule at submission completion. Consent-gated location/time should be explicit optional values, not inferred from upload metadata.

### Task lifecycle, audit, and schedule versions

**Analogs:** `src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs` (lines 8–94) and `src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs` (lines 18–127).

`ScheduleChangeProposal` validates stable identity/scope/version, keeps its transitions behind domain methods (`TryClaim`, `SetExplanation`, `Confirm`, `Fail`), tracks `StatusVersion`, actor/time of confirmation, and rejects stale or expired confirmations. The controller uses explicit farm authorization and returns `NotFound`, `Forbid`, or `Conflict` as distinct outcomes.

```csharp
if (Status != ScheduleChangeProposalStatus.Explained || version != Version ||
    evidenceVersion != EvidenceVersion || ExpiresAt <= now || string.IsNullOrWhiteSpace(producerId))
    return false;
```

**Apply to:** task/review state machines and schedule changes. Use monotonically increasing versions to detect stale clients; retain both payloads in a conflict record and require an explicit administrator resolution command. Schedule changes should append audit/history and increment version. Do not copy the schedule proposal's AI explanation fields into worker tasks.

### Idempotency, batches, outbox, and worker retries

**Analogs:** `src/AgroPlan.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs` (lines 7–21), `src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs` (lines 10–127), `src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs` (lines 16–141), and `src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs` (lines 15–380).

Outbox rows are persisted in PostgreSQL and claimed with `FOR UPDATE SKIP LOCKED` under a lease. Publish attempts update success/failure state; failures use bounded exponential backoff and eventually dead-letter. Consumers claim work in PostgreSQL before processing; integration coverage creates duplicate broker deliveries and races independent consumers to verify idempotency.

```csharp
.FromSqlInterpolated($"SELECT * FROM infra.outbox_messages WHERE ... LIMIT {BatchSize} FOR UPDATE SKIP LOCKED")
```

**Apply to:** persist every offline operation's client-generated ID and result atomically with its domain effect, protected by a unique constraint scoped to the authenticated worker/farm. A retry must return the original result without a second effect. Per-operation failures should be recorded without rolling back independent operations. Use transactional outbox events for email/notifications/calendar effects; do not publish directly during a task write. Database is authoritative and async delivery status remains visible/retryable.

### Provider-neutral unavailable contracts

**Analogs:** `src/AgroPlan.Application/Abstractions/IClimateProvider.cs` and `src/AgroPlan.Worker/Climate/UnconfiguredClimateProvider.cs`.

The application defines a provider abstraction and the current worker registers an explicitly unconfigured implementation, allowing APIs to report safe unavailability without fake data or a provider dependency.

**Apply to:** define a provider-neutral route/geography interface, route request/result geometry types, and an unconfigured implementation returning a clear unavailable state. Persist route version/stop order only when generation is supported. Do not select a production provider by analogy or activate synthetic geometry.

### Unit and integration test conventions

**Analogs:** `src/AgroPlan.UnitTests/Logbook/LogbookCorrectionTests.cs` (lines 7–43), `src/AgroPlan.IntegrationTests/Api/IdentityAndFarmAuthorizationTests.cs` (lines 13–178), `src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs` (lines 12–121), and `src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs`.

Use xUnit v3 `[Fact]`, purpose-named tests, `TestContext.Current.CancellationToken` in async tests, and PostgreSQL fixture collections for database-backed tests. API integration tests use a real test host, set bearer identity, assert HTTP results, then query PostgreSQL to verify the persisted state and tenant scope. Worker tests use PostgreSQL/RabbitMQ containers and deliberate duplicate event delivery.

**Apply to:** cover token expiry/single-use/revocation and plan cap race; farm/worker authorization and data visibility; evidence requiredness/consent/revision; verification-only wages/rewards; sync idempotent retries, batch partial failures, cross-farm rejection, stale conflict preservation and resolution; route unavailable/stale transitions; and calendar outbox retry/status. Unit-test domain transitions, then integration-test uniqueness, transactions, concurrency, permissions, and external delivery behavior.

## Shared Patterns

### Tenant and role security
Use the `ActiveFarmMembership` policy for current Owner/Administrator membership and a separate resource/role authorization path for workers. Match loaded resources by both `FarmId` and resource ID. Relevant examples: `FarmMembershipAuthorizationHandler.cs:17–32`, `FarmAuthorizationService.cs:7–13`, `ScheduleChangeProposalsController.cs:77–95`.

### Record integrity and audit
Use domain constructors and private setters for invariants; represent corrections/history as linked rows. Add database constraints/triggers for invariants that must hold outside the HTTP path. Relevant examples: `LogbookEntry.cs:14–56`, `CorrectLogbookEntry.cs:5–11`, `LogbookAndAttachmentsTests.cs:15–68`.

### Durable side effects
Commit business state and outbox messages together, then let the worker publish/send/update. Use claims/leases, idempotent consumers, retry state, and dead-letter status. Relevant examples: `OutboxPublisher.cs:58–127`, `ScheduleChangeProposalConsumer.cs:16–141`, `OutboxAndConsumerTests.cs`.

### API and test conventions
Use typed request/response records, data annotations or explicit validation, `CancellationToken`, farm-scoped routes, explicit HTTP conflict/not-found/forbidden outcomes, xUnit v3, PostgreSQL Testcontainers, and end-to-end assertions against stored rows.

## No Analog Found

| Capability | Reason |
|---|---|
| Worker roles and task assignment/review | No worker or task aggregate exists yet; schedule proposals are only a state-machine analogy. |
| Offline operation ledger and conflict resolution | No client operation IDs, sync endpoints, or conflict store currently exists. Design it from the requirements and concurrency/tenant constraints. |
| Route generation or provider integration | No geographic provider, geometry/routing contract, map cache, or route model exists. Keep generation explicitly unavailable pending approval. |
| Farm calendar external sync | Existing `CalendarScheduleItem` is internal schedule data; there is no external calendar provider or sync status adapter. Treat integration as a provider boundary and make retry state explicit. |
| Wage/payments, points and rewards | No payroll or gamification aggregate exists. Preserve “no payments” and award only verified work; use task-domain state transitions, not a payment analog. |

## Metadata

**Analog search scope:** `src/AgroPlan.Api`, `src/AgroPlan.Application`, `src/AgroPlan.Domain`, `src/AgroPlan.Infrastructure`, `src/AgroPlan.Worker`, `src/AgroPlan.UnitTests`, `src/AgroPlan.IntegrationTests`.  
**Pattern extraction date:** 2026-11-10
