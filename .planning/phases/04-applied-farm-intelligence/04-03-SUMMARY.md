---
phase: 04-applied-farm-intelligence
plan: 03
subsystem: api
tags: [aspnetcore, postgres, rabbitmq, private-storage, xunit-v3, scalar]
requires:
  - phase: 04-01
    provides: private media storage, file deletion, and authorization patterns
  - phase: 04-02
    provides: current farm authorization and optional provider job patterns
provides:
  - private photo-based symptom reports with bounded server-side image inspection and per-photo consent
  - provider-neutral observation-only Worker contract and outbox route
  - trusted agronomist verification and revocable farm-level review authorization
  - append-only human review decisions and retryable private-photo cleanup
affects: [04-04, ai-provider-activation, agronomist-verification-provisioning]
tech-stack:
  added: []
  patterns: [provider-neutral image observation, pre-provisioned identity verification, per-object deletion retry]
key-files:
  created:
    - src/AgroPlan.Api/Controllers/SymptomReportsController.cs
    - src/AgroPlan.Api/Controllers/AgronomistReviewController.cs
    - src/AgroPlan.Application/Abstractions/ISymptomObservationProvider.cs
    - src/AgroPlan.Domain/Symptoms/SymptomReport.cs
    - src/AgroPlan.Worker/Jobs/SymptomObservationConsumer.cs
    - src/AgroPlan.Worker/Jobs/SymptomPhotoCleanupService.cs
    - src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs
    - src/AgroPlan.IntegrationTests/Worker/SymptomReportCleanupTests.cs
    - src/AgroPlan.UnitTests/Symptoms/SymptomReportTests.cs
  modified:
    - src/AgroPlan.Application/Abstractions/IFileStorage.cs
    - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
    - src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs
    - src/AgroPlan.Worker/Program.cs
    - src/AgroPlan.IntegrationTests/IntegrationSupport.cs
key-decisions:
  - "Require explicit consent per photo before external processing; without an observation provider, keep reports pending for manual review and create no AI outbox job."
  - "Keep the observation provider contract limited to visible signs, uncertainty, image sufficiency, and requested evidence; diagnosis and treatment are human-review data only."
  - "Require a trusted pre-provisioned agronomist verification record plus a current, explicit farm assignment on every review and private-photo read."
  - "Requesting additional evidence preserves the pending report and its photos; confirmation or rejection queues private-photo deletion and retains review/audit history."
  - "Keep API docs available in Development through OpenAPI/Scalar and hidden in Production."
patterns-established:
  - "Image uploads are inspected by bounded BCL metadata parsing after storage and before finalization."
  - "External image jobs pass private bytes and minimum context only; report, membership, and per-photo consent are rechecked before retrieval, provider invocation, and persistence."
requirements-completed: [REQ-rf-g-43]
coverage:
  - id: D1
    description: Active farm members can upload private bounded symptom photos, finalize a pending report, and read scoped status without storage keys or public URLs.
    requirement: REQ-rf-g-43
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs#Finalized_private_photo_report_stays_pending_and_never_exposes_storage_keys
        status: pass
    human_judgment: false
  - id: D2
    description: Observation processing sends consented photo bytes only and persists visible-sign output without diagnosis or treatment.
    requirement: REQ-rf-g-43
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/SymptomReportCleanupTests.cs#Observation_consumer_sends_only_consented_photo_bytes_and_persists_observations
        status: pass
      - kind: unit
        ref: src/AgroPlan.UnitTests/Symptoms/SymptomReportTests.cs#SymptomReportTests
        status: pass
    human_judgment: false
  - id: D3
    description: Only a currently verified, explicitly assigned agronomist can request more evidence or confirm/reject a report; authorization revocation takes effect on the next request.
    requirement: REQ-rf-g-43
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs#Only_currently_verified_and_assigned_agronomist_can_review_and_read_private_photos
        status: pass
    human_judgment: false
  - id: D4
    description: Closed or rejected reports queue deletion of all private photos while preserving the report and review history.
    requirement: REQ-rf-g-43
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Worker/SymptomReportCleanupTests.cs#Rejected_report_deletes_private_photo_and_retains_report_and_review_history
        status: pass
    human_judgment: false
  - id: D5
    description: Development OpenAPI/Scalar exposes symptom-report routes and Production hides both documentation endpoints.
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs#SymptomReportOpenApiTests
        status: pass
    human_judgment: false
actuals:
  tokens: 28881
  tasks: 2
  commits: 5
plan_head_before: 72a56b2079b03922d9fcf8e43f7ae46d4c6a240f
plan_head_after: aadaf9a82ff702b81e6a3994de2efef9d1062072
commits: 5
metrics:
  duration: 44min
  completed: 2026-11-11
status: complete
---

# Phase 4 Plan 3: Private Symptom Reports Summary

**Private photo symptom reports now support bounded intake, observation-only processing, verified farm-scoped agronomist review, and retryable photo deletion.**

## Performance

- **Duration:** 44 minutes
- **Started:** 2026-11-11T15:31:00Z
- **Completed:** 2026-11-11T16:15:52Z
- **Tasks:** 2/2
- **Files changed:** 22

## Accomplishments

- Added private photo upload/finalization and scoped report reads with per-photo consent, JPEG/PNG/WebP metadata inspection, and enforced byte, edge, pixel, and photo-count bounds.
- Added a provider-neutral observation contract and RabbitMQ consumer that rechecks farm membership and consent, sends only private photo bytes and minimum crop context, and stores visible signs/evidence requests without AI diagnosis or treatment.
- Added trusted agronomist verification and farm-specific revocable authorization; requesting more evidence leaves the report pending and retains photos, while confirmation/rejection queues photo deletion and preserves the decision history.
- Added xUnit v3 unit, PostgreSQL/Testcontainers API/Worker, and OpenAPI/Scalar tests.

## Task Commits

1. **Task 1: Private symptom-report observation tracer** — `6f9f6a7` (RED test), `84ab17c` (RED evidence), `5ff6953` (implementation)
2. **Task 2: Verified agronomist review and close/reject photo deletion** — `83b902c` (review and cleanup coverage), `aadaf9a` (transactional observation claim)

The feature implementation commit also contains the shared agronomist domain and controller dependencies because the API, persistence configuration, and report lifecycle overlap both task slices.

## Decisions Made

- No observation provider is registered for this plan, so consented reports remain pending for manual review and the API does not enqueue an AI event.
- Trusted agronomist verification is pre-provisioned; the verification process itself is outside the report API and remains an activation dependency.
- The photo upload adapter uses the existing private storage contract and creates report-scoped private object keys.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Added symptom-specific storage authorization, routing, Worker registration, and integration fixture support**
- **Found during:** Task 1
- **Issue:** Existing generic upload authorization used logbook object paths, and RabbitMQ/Worker had no symptom observation queue or registration.
- **Fix:** Added a report-scoped private upload method, symptom queue routing, conditional Worker registration, and valid PNG fixture bytes.
- **Verification:** Focused API/Worker tests and full integration suite passed.
- **Committed in:** `5ff6953`

**2. [Rule 2 - Missing critical functionality] Added trusted agronomist review and durable photo cleanup components**
- **Found during:** Task 2
- **Issue:** Existing farm membership did not distinguish a verified agronomist or preserve auditable review and deletion lifecycle.
- **Fix:** Added pre-provisioned verification, explicit farm assignments, append-only decisions, and retryable photo deletion while retaining report history.
- **Verification:** API and cleanup Testcontainers tests passed.
- **Committed in:** `5ff6953`, with coverage in `83b902c`

**3. [Rule 2 - Missing critical functionality] Claimed observation jobs transactionally before provider calls**
- **Found during:** Final security review
- **Issue:** Duplicate RabbitMQ delivery could invoke the provider concurrently before a report entered Processing.
- **Fix:** Lock the report row, persist Processing before private-media retrieval, and return failed/revoked jobs to Pending.
- **Files modified:** `src/AgroPlan.Worker/Jobs/SymptomObservationConsumer.cs`, `src/AgroPlan.Domain/Symptoms/SymptomReport.cs`
- **Verification:** Full solution suite passed: 148 unit tests and 75 integration tests.
- **Committed in:** `aadaf9a`

**Total deviations:** 3 auto-fixed (2 Rule 2, 1 Rule 3)
**Impact on plan:** Required routing, storage, review, and cleanup wiring were added; no new package or external provider was introduced.

## Issues Encountered

- ASP.NET Core requires validation metadata on positional record constructor parameters. The upload and grant request records were corrected from property-targeted to parameter-targeted attributes.
- Testcontainers reused a database across tests, so storage fixture keys were made unique to avoid the upload-object uniqueness constraint.

## User Setup Required

None for the manual review path. Before AI observation can be activated, register a concrete `ISymptomObservationProvider`; before an agronomist can review, provision a trusted verification record through the separately defined identity-verification process.

## Next Phase Readiness

The manual symptom-report and verified review workflows are ready. AI observation remains disabled until a provider is registered. Phase 04-04 can verify cross-feature behavior and activation wiring.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReportTests" -m:1 -nr:false` — 5 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReport" -m:1 -nr:false` — 7 passed.
- `dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false --verbosity quiet` — 148 unit tests and 75 integration tests passed.

## Self-Check: PASSED

- Summary file exists at the required phase path.
- All five plan-range commits are present from `plan_head_before` through `plan_head_after`.
- No generated TestResults, `.planning/debug`, or pre-existing untracked workspace artifacts were staged.

---
*Phase: 04-applied-farm-intelligence*
*Completed: 2026-11-11*
