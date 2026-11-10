---
phase: 01-farm-planning-pilot
plan: 07
subsystem: api
tags: [dotnet, ef-core, postgresql, rabbitmq, agronomy]
requires:
  - phase: 01-farm-planning-pilot
    provides: farm authorization, crop cycles, async jobs, outbox, and proposal status patterns
provides:
  - provenance-backed validated crop framework model and deterministic capacity calculator
  - asynchronous farm-scoped frame proposal lifecycle with explicit owner/admin confirmation
  - explicit missing-input results and no capacity estimates when usable geometry/rules are unavailable
affects: [farm-planning-pilot, agronomy, crop-cycle]
actuals:
  tokens: 9154
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns: [dedicated outbox queue, deterministic sourced calculation, explicit proposal selection]
key-files:
  created:
    - src/AgroPlan.Domain/Crops/CropFramework.cs
    - src/AgroPlan.Domain/Planning/PlantingFrameProposal.cs
    - src/AgroPlan.Application/Planning/PlantingFrameCalculator.cs
    - src/AgroPlan.Application/UseCases/SubmitPlantingFrameProposal.cs
    - src/AgroPlan.Worker/Jobs/PlantingFrameConsumer.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/PlantingFrameProposal.cs
  modified:
    - src/AgroPlan.Api/Controllers/PlanningJobsController.cs
    - src/AgroPlan.Domain/Crops/CropCycle.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Worker/Program.cs
key-decisions:
  - "Do not seed numeric frame rules or use gross parcel area as usable area; return missing inputs until validated data exists."
  - "Persist explicit owner/administrator frame selection on both the proposal and crop cycle after rechecking active membership."
patterns-established:
  - "Only validated, sourced, currently valid framework records may produce deterministic capacity values."
  - "Frame jobs use a separate durable queue so sowing consumers cannot process the wrong event type."
requirements-completed: [REQ-pilot-planting-frame, REQ-rf-g-11, REQ-rf-g-38]
coverage:
  - id: D1
    description: Deterministic capacity calculation rejects missing validated rules or usable parcel geometry and emits named missing inputs.
    requirement: REQ-rf-g-11
    verification:
      - kind: other
        ref: "dotnet build src/AgroPlan.sln (.NET 10, compile-only)"
        status: pass
    human_judgment: true
    rationale: No behavioral tests or manual acceptance scenarios were run in this execution.
  - id: D2
    description: Farm-scoped frame job is persisted asynchronously and only active owners/administrators can explicitly select a returned option.
    requirement: REQ-pilot-planting-frame
    verification: []
    human_judgment: true
    rationale: API authorization and confirmation workflow need later acceptance review.
metrics:
  duration: 12m
  completed: 2026-11-10
  status: complete
plan_head_before: d9c7bb438256cf357ad991daf92cb718c8743036
plan_head_after: 69a8837488f28758242e375450d16ee8ad124a5f
commits: 2
---

# Phase 01 Plan 07: Deterministic planting frames Summary

**Validated frame rules now flow through deterministic capacity calculation and explicit farm-authorized selection, with no unsupported estimates.**

## Performance

- **Duration:** 12m
- **Started:** 2026-11-10T05:05:44Z
- **Completed:** 2026-11-10T05:17:40Z
- **Tasks:** 2
- **Files modified:** 17

## Accomplishments

- Added a provenance-backed crop framework model with validation status, region, and validity interval. No frame values were seeded for the five pilot crops.
- Added a deterministic calculator that returns no capacity until validated spacing, usable parcel area, and validated aisle geometry are available.
- Added asynchronous frame proposal submit/get/confirm API routes, a dedicated RabbitMQ queue and idempotent pending-only consumer, plus current owner/administrator authorization on confirmation.
- Persisted a confirmed selection on the crop cycle only when its framework option appears in the completed proposal and the proposal has no missing inputs.

## Task Commits

1. **Task 1: Calculate candidate frame capacities from validated inputs only** - `aa037a2` (feat)
2. **Task 2: Offer farm-authorized frame proposals and explicit selection** - `69a8837` (feat)

## Files Created/Modified

- `src/AgroPlan.Domain/Crops/CropFramework.cs` - sourced frame-spacing and aisle rule model.
- `src/AgroPlan.Application/Planning/PlantingFrameCalculator.cs` - deterministic capacity or missing-input result.
- `src/AgroPlan.Domain/Planning/PlantingFrameProposal.cs` - proposal lifecycle and explicit option selection.
- `src/AgroPlan.Api/Controllers/PlanningJobsController.cs` - submit, inspect, and confirm endpoints.
- `src/AgroPlan.Worker/Jobs/PlantingFrameConsumer.cs` - async deterministic evaluation.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/PlantingFrameProposal.cs` - schema for sourced rules, proposals, and crop-cycle selection.

## Decisions Made

- The current parcel model has gross hectares but no usable area or aisle geometry. The worker does not substitute gross area and reports `parcel.usableAreaSquareMeters` and `parcel.aisleGeometry` as missing.
- No region-specific framework is used unless the crop catalog region matches it.
- Gemini was not called for the current no-options case; AI output cannot supply missing geometry, spacing, or capacity.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Kept confirmation from recording a frame without a selectable sourced option**
- **Found during:** Task 2
- **Issue:** A completed job alone is insufficient grounds to attach a framework to the crop cycle.
- **Fix:** Confirmation now verifies an empty missing-input list and membership of the selected framework in the proposal options; the endpoint rechecks active owner/administrator membership.
- **Files modified:** `src/AgroPlan.Domain/Planning/PlantingFrameProposal.cs`, `src/AgroPlan.Api/Controllers/PlanningJobsController.cs`
- **Verification:** Compile-only .NET 10 build passed.
- **Committed in:** `69a8837`

**2. [Rule 3 - Blocking] Routed planting-frame messages to their own queue**
- **Found during:** Task 2
- **Issue:** The existing publisher sent all outbox messages to the sowing queue, where the sowing consumer would reject frame events.
- **Fix:** Added a durable planting-frame queue and consumer, and marked proposals failed if frame-event delivery exhausts retries.
- **Files modified:** `src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs`, `src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs`, `src/AgroPlan.Worker/Jobs/PlantingFrameConsumer.cs`
- **Verification:** Compile-only .NET 10 build passed.
- **Committed in:** `69a8837`

---

**Total deviations:** 2 auto-fixed (1 missing critical, 1 blocking)
**Impact on plan:** Preserved the planned workflow and added safeguards needed to ensure unsupported or misrouted data cannot be confirmed.

## Issues Encountered

- Current pilot data contains no validated framework rules and the parcel model has no usable-area/aisle-geometry fields. The endpoint therefore produces explicit missing inputs with no capacity figures, as required by the accepted agronomy constraints.
- Compile-only build passed using the local .NET 10 SDK. No tests were run and no migrations were applied.

## User Setup Required

None - no external provider setup is required for the deterministic missing-data path.

## Next Phase Readiness

- Frame proposal API and persistence are in place. Before production capacity outputs can appear, validated region-specific rules and parcel usable-area/aisle geometry must be added through reviewed source data.

## Self-Check: PASSED

- Both task commits are ancestors of the recorded plan head.
- The summary and implementation files exist.

---
*Phase: 01-farm-planning-pilot*
*Completed: 2026-11-10*
