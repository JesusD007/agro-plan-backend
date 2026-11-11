---
phase: 04-applied-farm-intelligence
plan: 01
subsystem: api
tags: [aspnet-core, xunit-v3, postgres, supabase, voice, scalar]
requires:
  - phase: 01-foundation
    provides: farm-scoped membership checks, outbox, private Supabase storage
  - phase: 03-workforce-and-offline-operations
    provides: assigned-worker task authorization and immutable logbook patterns
provides:
  - Provider-neutral speech contract and distinct Dominican Spanish/Haitian Creole language values
  - Farm-scoped consented voice draft API, PostgreSQL/outbox persistence, and Worker processing
  - Explicit confirmation to immutable logbook and retryable seven-day private audio cleanup
  - xUnit v3 unit, PostgreSQL/Worker, and Scalar/OpenAPI coverage
affects: [04-02, 04-03, 04-04, voice, ai, storage]
actuals:
  tokens: 30468
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns: [provider-neutral speech port, consent-per-upload, explicit-confirmation, retryable-private-media-cleanup]
key-files:
  created:
    - src/AgroPlan.Api/Controllers/VoiceDraftsController.cs
    - src/AgroPlan.Application/Abstractions/ISpeechTranscriptionProvider.cs
    - src/AgroPlan.Domain/Voice/VoiceActivityDraft.cs
    - src/AgroPlan.Worker/Jobs/VoiceDraftConsumer.cs
    - src/AgroPlan.Worker/Jobs/ExpiredVoiceAudioCleanupService.cs
    - src/AgroPlan.IntegrationTests/Api/VoiceDraftIntegrationTests.cs
    - src/AgroPlan.UnitTests/Voice/VoiceActivityDraftTests.cs
  modified:
    - src/AgroPlan.Application/Abstractions/IFileStorage.cs
    - src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/VoiceActivityDraftConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/VoiceActivityDrafts.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.IntegrationTests/IntegrationSupport.cs
key-decisions:
  - "Use a provider-neutral speech interface; keep production speech integration disabled pending both language gates."
  - "Require explicit consent for each recording and recheck farm membership, task assignment, and consent before processing and publishing."
  - "Only explicit authorized confirmation creates an immutable logbook entry; preserve uncertain values as null."
  - "Keep audio private and delete at confirmation or within seven days after processing, with durable retries."
patterns-established:
  - "Speech providers receive only audio bytes and selected language, never farm or user identifiers."
  - "Cleanup claims and records per-object deletion outcome so storage retries do not duplicate logbook writes."
requirements-completed: [REQ-rf-g-39]
coverage:
  - id: D1
    description: Provider-neutral, consented voice draft lifecycle with assignment checks and explicit logbook confirmation
    requirement: REQ-rf-g-39
    verification:
      - kind: unit
        ref: "dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~VoiceActivityDraftTests -m:1 -nr:false"
        status: unknown
      - kind: integration
        ref: "dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~VoiceDraftIntegrationTests -m:1 -nr:false"
        status: unknown
    human_judgment: true
    rationale: VSTest could not connect to its testhost; the test cases did not start and require a rerun in a working test runner environment.
  - id: D2
    description: Scalar/OpenAPI documents voice routes in Development and hides documentation in Production
    verification:
      - kind: integration
        ref: "VoiceDraftOpenApiTests"
        status: unknown
    human_judgment: true
    rationale: The testhost startup issue prevented execution of the OpenAPI test.
  - id: D3
    description: Confirmed and expired voice audio is privately deleted with durable retry state
    verification:
      - kind: integration
        ref: "VoiceDraftIntegrationTests.Confirmed_audio_is_deleted_and_audit_record_remains"
        status: unknown
    human_judgment: true
    rationale: PostgreSQL/Testcontainers cleanup coverage compiled but could not run because VSTest failed before fixture startup.
duration: 45min
completed: 2026-11-11
status: complete
commits: 2
plan_head_before: b6b16e75ce8083177e4a02da6b22fe7c5bc33e52
plan_head_after: 1e0f0cb84293f4e0a9f579ef677a237890c528e8
---

# Phase 4 Plan 1: Voice Activity Drafts Summary

**Provider-neutral voice drafts now flow from consented private recording through editable transcription to one-time immutable logbook confirmation, with retryable audio deletion.**

## Performance

- **Duration:** 45 min
- **Started:** 2026-11-11
- **Completed:** 2026-11-11
- **Tasks:** 2
- **Files modified:** 15

## Accomplishments

- Implemented the provider-neutral speech contract, validated JSON results, and bounded WAV inspection for Dominican Spanish and Haitian Creole.
- Added farm-scoped voice upload, consent and assignment checks, outbox processing, editable partial drafts, and explicit confirmation.
- Added a durable cleanup worker and xUnit v3 coverage for domain, API, PostgreSQL/Worker, and Scalar/OpenAPI behavior.

## Task Commits

1. **Task 1: Voice draft tracer from private recording to explicit logbook confirmation** - `327b269` (feat)
2. **Task 2: Seven-day audio expiry and retryable deletion** - `1e0f0cb` (feat)

## Verification

- `dotnet build src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 -nr:false` — passed, 0 warnings and 0 errors in the latest build before the final added cleanup/OpenAPI cases; a later rebuild also passed with 0 errors and existing xUnit1051 warnings in unrelated tests.
- `dotnet build src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1 -nr:false` — passed, 0 warnings and 0 errors.
- Unit filter `VoiceActivityDraftTests` — exit 1 before test execution: VSTest failed to connect to testhost after 180 seconds; PID 24372 was still running.
- Integration filter `VoiceDraftIntegrationTests` — exit 1 before test execution: VSTest failed to connect to testhost after 180 seconds; PID 27428 was still running. PostgreSQL/Testcontainers did not start, so Docker permissions were not reached.

## Deviations from Plan

The voice intake policy, RabbitMQ queue mapping, EF DbSet registration, and integration storage fake were also updated so the new vertical slice can compile and exercise its contracts. Dedicated `VoiceDraftOpenApiTests` and confirmed-audio cleanup coverage were added. Both requested test runs remain unverified because the VSTest host never connected; this is recorded as an unrun verification, not a test assertion failure.

## Issues Encountered

- Focused unit and integration test processes both failed at VSTest-to-testhost startup after the configured 180-second timeout. The integration filter never reached Docker/Testcontainers.
- Integration test build reported pre-existing xUnit1051 cancellation-token analyzer warnings in other test files; no build errors occurred.

## Next Phase Readiness

Plan 04-01 implementation is complete. Re-run the focused xUnit filters in a functioning testhost environment before relying on their assertions. Provider activation and cleanup Worker host registration remain assigned to plan 04-04.

## Self-Check: PASSED

- Summary exists at the expected plan path.
- Task commits `327b269` and `1e0f0cb` are ancestors of the current `develop` HEAD.
- Two measured plan commits match `git rev-list --count plan_head_before..HEAD`.

---
*Phase: 04-applied-farm-intelligence*
*Completed: 2026-11-11*
