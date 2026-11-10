---
phase: 02-farm-operations-and-safety
plan: 08
subsystem: api
tags: [aspnetcore, postgres, rabbitmq, outbox, gemini, xunit-v3]
requires:
  - phase: 02-03
    provides: Safe advisory source contracts and freshness-aware providers
  - phase: 02-04
    provides: Deterministic harvest safety gate
  - phase: 02-05
    provides: Validated crop calendar and confirmed planting/progress data
provides:
  - Farm-scoped immutable schedule proposals with evidence version snapshots
  - Durable schedule explanation processing through PostgreSQL outbox and RabbitMQ
  - Provider-neutral calendar explanation operation with Gemini Worker adapter
  - Producer confirmation that applies deterministic schedule dates and projects them in calendar reads
affects: [phase-2-verification, calendar, worker, ai-provider]
actuals:
  tokens: 21586
  tasks: 2
  commits: 3
plan_head_before: 9af0d623d1635cbe4459c6db8ad2535aceea4091
plan_head_after: 9a7ca3035ced80da101de967e44a6585a46c6abc
tech-stack:
  added: []
  patterns: [provider-neutral AI contract, idempotent conditional worker claim, immutable proposal snapshot and confirmed schedule projection]
key-files:
  created:
    - src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs
    - src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs
    - src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/ScheduleChangeProposals.cs
    - src/AgroPlan.Application/Calendars/CalendarExplanationDraftValidator.cs
  modified:
    - src/AgroPlan.Application/Abstractions/IAiProposalProvider.cs
    - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
    - src/AgroPlan.Api/Controllers/CropCalendarsController.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
requirements-completed: [REQ-rf-g-13, REQ-rf-g-42]
coverage:
  - id: D1
    description: Producers can create an evidence-versioned proposal and must confirm before deterministic dates are applied to the farm calendar.
    requirement: REQ-rf-g-13
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs#Schedule_change_is_durable_explained_once_and_requires_producer_confirmation
        status: pass
    human_judgment: false
  - id: D2
    description: Gemini explanations use a provider-neutral contract and are checked for evidence, unsupported numeric/date claims, safety claims and injection-like text.
    requirement: REQ-rf-g-13
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Calendars/CalendarExplanationValidationTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Calendar reads project only the exact proposal version confirmed by a farm producer.
    requirement: REQ-rf-g-42
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs#Schedule_change_is_durable_explained_once_and_requires_producer_confirmation
        status: pass
    human_judgment: false
key-decisions:
  - "The deterministic crop rule and confirmed planting evidence remain authoritative; the AI only explains a bounded proposal."
  - "Proposal task dates are applied only by producer confirmation after rechecking current membership, evidence version, proposal version and expiry."
  - "Gemini remains the current Worker implementation behind IAiProposalProvider; tests use a fake provider."
patterns-established:
  - "Schedule proposals store immutable deltas/evidence while status, validated explanation provenance and confirmation metadata advance through the lifecycle."
  - "Worker claims use a conditional database update so duplicate deliveries do not invoke the provider twice."
metrics:
  duration: 22m
  completed: 2026-11-10
duration: 22m
completed: 2026-11-10
status: complete
---

# Phase 2 Plan 8: Schedule Proposals and AI Explanation Summary

**Confirmed, version-checked calendar proposals apply deterministic dates; Gemini supplies only a grounded advisory explanation through a provider-neutral contract.**

## Performance

- **Duration:** 22m
- **Tasks:** 2
- **Files modified:** 16

## Accomplishments

- Added farm-scoped schedule proposal, retrieval and producer-confirmation endpoints with proposal/evidence version checks and current membership validation.
- Added durable outbox routing, idempotent RabbitMQ worker processing, a confirmation-backed calendar schedule projection and current template/evidence checks.
- Extended `IAiProposalProvider` with a calendar explanation contract and Gemini adapter; added grounding, injection-like text, numeric/date and safety-claim tests.

## Task Commits

1. **Task 1: Persist deterministic schedule proposals and require producer confirmation** — `f7790aa`
2. **Task 2: Add provider-neutral AI explanations and offline safety evaluation** — `e11edf5`
3. **Task 1 follow-up: Apply confirmed calendar deltas** — `9a7ca30`

## Deviations from Plan

- Added `calendar_schedule_items` to persist the producer-confirmed calendar projection because the existing calendar template is immutable; without this state, confirmation would not affect calendar reads. This closes the confirmation-to-calendar key link.
- TDD RED/GREEN commit evidence was not recorded; the final focused xUnit v3 and PostgreSQL/RabbitMQ integration tests passed.
- The full 20-case expert-labeled activation dataset and production activation gates in `02-AI-SPEC.md` remain production prerequisites; no live Gemini credentials or calls were used.

## Verification

- `dotnet build src/AgroPlan.sln --no-restore -m:1` — passed, 0 errors.
- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~CalendarExplanation" --no-build --no-restore -m:1` — 6 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~OutboxAndConsumerTests.Schedule_change" --no-build --no-restore -m:1` — 1 passed.
- NuGet emitted `NU1900` warnings because `api.nuget.org` was unreachable for vulnerability metadata. The solution build also reports existing xUnit cancellation analyzer warnings.

## User Setup Required

Before production activation, configure the approved Gemini credentials/model, obtain agronomist approval for the Spanish explanation corpus, and satisfy the evaluation, privacy, retention, monitoring and disable-control gates documented in `02-AI-SPEC.md`.

## Self-Check: PASSED

- All created plan artifacts exist.
- Commits `f7790aa`, `e11edf5` and `9a7ca30` are reachable from `develop`.
- Summary actuals are measured from the plan-start HEAD ledger.
