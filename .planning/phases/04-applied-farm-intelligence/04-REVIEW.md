---
phase: 04-applied-farm-intelligence
reviewed: 2026-11-11T16:53:00Z
depth: standard
files_reviewed: 48
files_reviewed_list:
  - src/AgroPlan.Api/Controllers/AgronomistReviewController.cs
  - src/AgroPlan.Api/Controllers/SymptomReportsController.cs
  - src/AgroPlan.Api/Controllers/VoiceDraftsController.cs
  - src/AgroPlan.Api/Controllers/WeeklySummariesController.cs
  - src/AgroPlan.Api/Program.cs
  - src/AgroPlan.Application/Abstractions/IFileStorage.cs
  - src/AgroPlan.Application/Abstractions/ISpeechTranscriptionProvider.cs
  - src/AgroPlan.Application/Abstractions/ISymptomObservationProvider.cs
  - src/AgroPlan.Application/Abstractions/IWeeklySummaryNarrativeProvider.cs
  - src/AgroPlan.Application/Reports/WeeklySummaryProjection.cs
  - src/AgroPlan.Domain/Reports/WeeklySummary.cs
  - src/AgroPlan.Domain/Symptoms/FarmAgronomistAuthorization.cs
  - src/AgroPlan.Domain/Symptoms/SymptomReport.cs
  - src/AgroPlan.Domain/Symptoms/TrustedAgronomistVerification.cs
  - src/AgroPlan.Domain/Voice/VoiceActivityDraft.cs
  - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
  - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmAgronomistAuthorizationConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/SymptomReportConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/TrustedAgronomistVerificationConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/VoiceActivityDraftConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/WeeklySummaryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/SymptomAgronomistAuthorization.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/SymptomReports.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/VoiceActivityDrafts.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/WeeklySummaries.cs
  - src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs
  - src/AgroPlan.IntegrationTests/Api/AppliedFarmIntelligenceContractTests.cs
  - src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs
  - src/AgroPlan.IntegrationTests/Api/VoiceDraftIntegrationTests.cs
  - src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs
  - src/AgroPlan.IntegrationTests/IntegrationSupport.cs
  - src/AgroPlan.IntegrationTests/Worker/AppliedFarmIntelligenceWorkerActivationTests.cs
  - src/AgroPlan.IntegrationTests/Worker/SymptomReportCleanupTests.cs
  - src/AgroPlan.IntegrationTests/Worker/WeeklySummaryConsumerTests.cs
  - src/AgroPlan.UnitTests/AI/Phase04EvaluationCases.cs
  - src/AgroPlan.UnitTests/AI/Phase04EvaluationTests.cs
  - src/AgroPlan.UnitTests/Reports/WeeklySummaryProjectionTests.cs
  - src/AgroPlan.UnitTests/Symptoms/SymptomReportTests.cs
  - src/AgroPlan.UnitTests/Voice/VoiceActivityDraftTests.cs
  - src/AgroPlan.Worker/AI/UnconfiguredSpeechTranscriptionProvider.cs
  - src/AgroPlan.Worker/AI/UnconfiguredSymptomObservationProvider.cs
  - src/AgroPlan.Worker/AI/UnconfiguredWeeklySummaryProvider.cs
  - src/AgroPlan.Worker/AppliedFarmIntelligenceWorkerRegistration.cs
  - src/AgroPlan.Worker/Jobs/ExpiredVoiceAudioCleanupService.cs
  - src/AgroPlan.Worker/Jobs/SymptomObservationConsumer.cs
  - src/AgroPlan.Worker/Jobs/SymptomPhotoCleanupService.cs
  - src/AgroPlan.Worker/Jobs/VoiceDraftConsumer.cs
  - src/AgroPlan.Worker/Jobs/WeeklySummaryConsumer.cs
  - src/AgroPlan.Worker/Program.cs
findings:
  critical: 3
  warning: 2
  info: 0
  total: 5
  necessity: 0
status: issues_found
---

# Phase 04: Code Review Report

**Reviewed:** 2026-11-11T16:53:00Z  
**Depth:** standard  
**Files Reviewed:** 48  
**Status:** issues_found

## Summary

Reviewed the Phase 4 source scope from the phase task commits, including API authorization, external-media consent, provider job activation, worker revalidation, persistence contracts, and tests. Three blockers affect farm data confidentiality or prevent the agronomist-requested evidence flow from completing. Two additional warnings affect retry correctness and activation of provider-backed API jobs. Phase summaries report passing tests, but those results do not cover the identified authorization and workflow gaps.

## Critical Issues

### CR-01: Advisor receives the unfiltered summary narrative

**Classification:** BLOCKER  
**File:** `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs:144-148`  
**Issue:** The advisor response filters `Projection` according to the active grant scopes, but returns `summary.NarrativeText` unchanged. The narrative is generated from all cost and production sources/totals, so a costs-only advisor can still read production facts in prose and a production-only advisor can read costs. This defeats the scope check even though source rows and totals are filtered.
**Fix:** Persist narrative facts in a structured form and render/filter it under the grant scopes, or omit the narrative for advisors unless it can be safely regenerated from the filtered projection. Add tests for each single-scope grant where the ungranted category appears only in narrative text.

### CR-02: Report finalization succeeds after farm membership is revoked

**Classification:** BLOCKER  
**File:** `src/AgroPlan.Api/Controllers/SymptomReportsController.cs:63-70`  
**Issue:** `FinalizeReport` verifies only that the caller is the original submitter. Unlike upload authorization and report reads, it does not query the caller's active farm role. A worker/administrator whose membership was revoked can still finalize a report and cause its state transition and possible AI outbox event. This violates the project's per-request membership-revocation rule.
**Fix:** Resolve and require an active permitted farm role before loading/finalizing the report, then revalidate in the same transaction that changes report state and emits the outbox event. Add a regression test revoking membership between upload authorization and finalization.

### CR-03: Requested additional evidence cannot be uploaded

**Classification:** BLOCKER  
**File:** `src/AgroPlan.Api/Controllers/SymptomReportsController.cs:43-47`; `src/AgroPlan.Domain/Symptoms/SymptomReport.cs:127-131`  
**Issue:** Upload authorization for an existing report accepts it only while its status is `Uploading`. Finalization moves it to `Pending`; when an agronomist requests more evidence, the state also remains `Pending`. No operation reopens the report or permits an additional photo in that state. The report therefore cannot satisfy the agronomist's request or proceed to a new review with additional images.
**Fix:** Add an explicit, authorized evidence-submission transition that reopens a report for additional uploads, preserves prior photos/decisions, and returns it to pending review after finalization. Cover request-more-evidence → upload → finalize → re-review end to end.

## Warnings

### WR-01: Retry overcounts pixels for photos already finalized

**Classification:** WARNING  
**File:** `src/AgroPlan.Api/Controllers/SymptomReportsController.cs:72-86`  
**Issue:** If finalization previously moved some photos but failed before the database transaction committed, a retry counts each finalized photo as `MaximumPixelsPerImage` rather than its inspected pixel count. Valid reports can consequently be rejected at the aggregate pixel limit on retry. The actual dimensions are not persisted, so the retry cannot reconstruct the correct total.
**Fix:** Persist validated width/height or pixel count with each finalized photo and use that stored value on retries. Add a test with multiple valid images where a partial finalization is retried.

### WR-02: API never enqueues weekly-summary or symptom AI work

**Classification:** WARNING  
**File:** `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs:113-119`; `src/AgroPlan.Api/Controllers/SymptomReportsController.cs:94-101`; `src/AgroPlan.Api/Program.cs:119-130`  
**Issue:** Both controllers emit AI outbox messages only when their provider interface is registered in the API container. The API registration block configures storage and other providers but does not register `IWeeklySummaryNarrativeProvider` or `ISymptomObservationProvider`; those interfaces are registered in the separate Worker host. As a result, enabling/configuring a provider in the worker cannot make these API branches enqueue work, leaving summaries without narratives and symptom reports without AI observations.
**Fix:** Base job creation on an explicit feature activation/configuration contract shared by API and Worker, not the API process's provider instance. Ensure the configuration that enables the worker also enables durable outbox emission, and test the API + Worker composition together.

---

_Reviewed: 2026-11-11T16:53:00Z_  
_Reviewer: the agent (gsd-code-reviewer)_  
_Depth: standard_
