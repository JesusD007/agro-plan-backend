---
phase: 01-farm-planning-pilot
plan: 06
subsystem: proposal-generation
tags: [gemini, climate, worker, confirmation]
dependency-graph:
  requires: [01-05]
  provides: [gemini-draft-provider, climate-contract, proposal-confirmation]
  affects: [worker, planning-api, sowing-proposals]
tech-stack:
  added: [Google.GenAI-1.28.0]
  patterns: [server-side-secret, structured-output-validation, bounded-retry, circuit-breaker, explicit-human-confirmation]
key-files:
  created:
    - src/AgroPlan.Application/Abstractions/IClimateProvider.cs
    - src/AgroPlan.Application/Abstractions/IGeminiProposalProvider.cs
    - src/AgroPlan.Application/UseCases/ConfirmProposal.cs
    - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
    - src/AgroPlan.Worker/Climate/UnconfiguredClimateProvider.cs
    - src/AgroPlan.Worker/appsettings.Development.example.json
    - src/AgroPlan.Infrastructure/Persistence/Migrations/SowingProposalConfirmation.cs
  modified:
    - src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs
    - src/AgroPlan.Worker/Program.cs
    - src/AgroPlan.Worker/AgroPlan.Worker.csproj
    - src/AgroPlan.Api/Controllers/PlanningJobsController.cs
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Domain/Planning/SowingProposal.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/SowingProposalConfiguration.cs
decisions:
  - Pin Google.GenAI 1.28.0, matching the official tagged C# SDK release and .NET 10 compatibility.
  - Keep climate unavailable until a user-selected source is approved; incomplete drafts cannot be confirmed.
metrics:
  duration: 18m
  completed: 2026-11-10
  tasks: 2
  commits: 2
  plan_head_before: ef278ba
  plan_head_after: 3f005ff
  actuals:
    tokens: 11105
    tasks: 2
    commits: 2
status: complete
---

# Phase 1 Plan 6: Gemini and Climate Adapters Summary

The Worker now produces structured Gemini drafts with source provenance and explicit missing inputs, while requiring a current owner/administrator to confirm a complete proposal.

## What Changed

- Added application contracts for climate inputs/results and Gemini sowing draft generation.
- Integrated the official Google.GenAI SDK, reading `GEMINI_API_KEY` from server-side configuration and the model from `GEMINI_MODEL` or `Gemini:Model`.
- Validated model JSON structure, source identifiers, summary length, and unsupported numeric output. Added a bounded timeout, one transient retry, and circuit breaker.
- Added a climate provider that reports unavailable status and missing provider/coordinates until a production source is selected.
- Connected the Worker to persist the input snapshot, model/prompt/schema versions, source records, climate context, and missing inputs with draft status.
- Added authorized review response and a confirm endpoint that rechecks active membership; incomplete proposals remain unconfirmable. Added persisted confirmer/timestamp metadata and a migration. Migration was not applied.

## Verification

- `.NET 10` solution build completed successfully: 0 warnings, 0 errors (run by parent orchestrator after source completion).
- No tests were added or run, per task instruction.
- No deployment or migration apply performed.

## Deviations from Plan

**1. Added a migration for confirmation audit metadata.**
- **Found during:** Task 3
- **Issue:** Confirmed state requires durable confirmer and timestamp fields to meet audit requirements.
- **Fix:** Added `SowingProposalConfirmation` migration; it is committed but unapplied.
- **Files modified:** `src/AgroPlan.Infrastructure/Persistence/Migrations/SowingProposalConfirmation.cs`, `src/AgroPlan.Domain/Planning/SowingProposal.cs`, `src/AgroPlan.Infrastructure/Persistence/Configurations/SowingProposalConfiguration.cs`

## Known Stubs

| File | Reason |
|---|---|
| `src/AgroPlan.Worker/Climate/UnconfiguredClimateProvider.cs` | Intentional: no weather source has been selected or approved, so it returns unavailable and missing-input metadata. |

## Self-Check: PASSED

- Provider contracts, adapters, confirm use case, migration, and this summary exist.
- Task commits `8cecdf2` and `3f005ff` are present in branch history.
- Build passed after the Worker exception namespace import was corrected.
