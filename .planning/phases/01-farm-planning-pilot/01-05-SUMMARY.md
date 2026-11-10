---
phase: 01-farm-planning-pilot
plan: 05
subsystem: async-sowing
tags: [outbox, rabbitmq, worker, idempotency, signalr]
dependency_graph:
  requires: [01-04]
  provides: [durable-sowing-jobs, confirmed-outbox-publishing, idempotent-worker-consumption, farm-scoped-job-status]
  affects: [01-06, 01-07]
tech_stack:
  added: [RabbitMQ.Client 7.2.2, SignalR]
  patterns: [transactional-outbox, publisher-confirms, manual-acknowledgement, PostgreSQL-authoritative-state, per-user-hub-groups]
key_files:
  created:
    - src/AgroPlan.Api/Controllers/PlanningJobsController.cs
    - src/AgroPlan.Api/Hubs/JobStatusHub.cs
    - src/AgroPlan.Api/Services/JobStatusNotificationService.cs
    - src/AgroPlan.Application/UseCases/SubmitSowingPlan.cs
    - src/AgroPlan.Domain/Planning/SowingProposal.cs
    - src/AgroPlan.Domain/Planning/OutboxMessage.cs
    - src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqConnection.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/SowingJobsOutbox.cs
    - src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Worker/Program.cs
decisions:
  - Store the pending job and outbox event in the same PostgreSQL transaction; publish only the stable job ID.
  - Require RabbitMQ publisher confirms and persistent messages; worker uses manual acknowledgements and durable idempotent state transitions.
  - SignalR groups are per user and active membership is queried from PostgreSQL for every notification; clients must retrieve job state from the API.
  - Keep climate and agronomic inputs explicitly missing until validated providers/rules are added in later plans.
metrics:
  duration: 75 min
  completed_date: 2026-11-10
  commits: 2
  plan_head_before: 4cb3b899b44045b6a43a60de6060369c89cdb69f
  plan_head_after: 4bab07bebe34708ad54ebb083d112e2cab8aa23d
  status: complete
actuals:
  tokens: 12025
  tasks: 2
  commits: 2
---

# Phase 1 Plan 5: Durable Sowing Job and Outbox Lifecycle Summary

Farm-authorized sowing requests now persist a pending proposal and outbox event atomically, flow through confirmed RabbitMQ delivery and idempotent worker state transitions, and expose PostgreSQL-backed status with advisory SignalR updates.

## What Changed

- Added a farm-scoped submission endpoint that verifies active membership, crop-cycle ownership, and parcel ownership before saving the job and outbox record in one transaction. The API exposes durable job status retrieval under the same farm authorization policy.
- Added an outbox publisher with leased batch claims, stable event identifiers, persistent RabbitMQ messages, publisher confirms, and a three-attempt retry budget with jitter.
- Added a worker that uses prefetch one and manual acknowledgements, ignores duplicate terminal jobs, and persists results or explicit failure before acknowledging.
- Added a SignalR hub that verifies current membership on `JoinFarm`; notifications are sent only to per-user groups for currently active owners and administrators. SignalR remains advisory and job state remains in PostgreSQL.
- Added the PostgreSQL migration and EF mappings for proposals and outbox messages. The migration was created but not applied.
- No climate source or validated agronomic parameters are enabled. Requests carry explicit missing-input markers and no invented recommendations.

## Verification

- Compile-only solution build passed with the local .NET 10 SDK: all projects built, 0 warnings and 0 errors. This was confirmed by the root orchestrator's elevated restore/build after sandbox-local no-restore attempts returned a silent MSBuild failure.
- No tests were run or added. No database migration was applied, no broker connection was configured, and no deployment occurred.

## Known Stubs

| File | Reason |
|------|--------|
| `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs` | Proposal generation intentionally records `PROPOSAL_PROVIDER_NOT_CONFIGURED` until Plan 01-06 supplies the Gemini/provider pipeline; no fabricated draft is emitted. |

## Deviations from Plan

None. The provider-not-configured terminal failure keeps this plan's durable lifecycle truthful while proposal generation is implemented by the dependent plan.

## Self-Check: PASSED

- Summary exists at the expected phase path.
- Both task commits are ancestors of the current plan branch: `d3158ad`, `4bab07b`.
- Commit count was measured from the persisted plan ledger: 2.
