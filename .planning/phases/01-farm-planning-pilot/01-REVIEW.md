---
phase: 01-farm-planning-pilot
reviewed: 2026-11-10T15:00:00Z
depth: deep
files_reviewed: 95
files_reviewed_list:
  - .dockerignore
  - .gitignore
  - Dockerfile.api
  - Dockerfile.worker
  - src/AgroPlan.Api/AgroPlan.Api.csproj
  - src/AgroPlan.Api/Authorization/FarmMembershipAuthorizationHandler.cs
  - src/AgroPlan.Api/Controllers/CropCyclesController.cs
  - src/AgroPlan.Api/Controllers/FarmsController.cs
  - src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs
  - src/AgroPlan.Api/Controllers/LogbookController.cs
  - src/AgroPlan.Api/Controllers/MembershipsController.cs
  - src/AgroPlan.Api/Controllers/ParcelsController.cs
  - src/AgroPlan.Api/Controllers/PlanningJobsController.cs
  - src/AgroPlan.Api/Controllers/SupportRecoveryController.cs
  - src/AgroPlan.Api/Hubs/JobStatusHub.cs
  - src/AgroPlan.Api/Identity/IdentityEndpoints.cs
  - src/AgroPlan.Api/Identity/TokenIssuer.cs
  - src/AgroPlan.Api/Program.cs
  - src/AgroPlan.Api/Services/JobStatusNotificationService.cs
  - src/AgroPlan.Api/appsettings.Development.example.json
  - src/AgroPlan.Api/appsettings.json
  - src/AgroPlan.Application/Abstractions/IClimateProvider.cs
  - src/AgroPlan.Application/Abstractions/IEmailSender.cs
  - src/AgroPlan.Application/Abstractions/IFarmAuthorization.cs
  - src/AgroPlan.Application/Abstractions/IFileStorage.cs
  - src/AgroPlan.Application/Abstractions/IGeminiProposalProvider.cs
  - src/AgroPlan.Application/AgroPlan.Application.csproj
  - src/AgroPlan.Application/Crops/CropCatalogService.cs
  - src/AgroPlan.Application/Crops/ICropCatalogRepository.cs
  - src/AgroPlan.Application/Planning/PlantingFrameCalculator.cs
  - src/AgroPlan.Application/UseCases/AppendLogbookEntry.cs
  - src/AgroPlan.Application/UseCases/ConfirmProposal.cs
  - src/AgroPlan.Application/UseCases/CorrectLogbookEntry.cs
  - src/AgroPlan.Application/UseCases/SubmitPlantingFrameProposal.cs
  - src/AgroPlan.Application/UseCases/SubmitSowingPlan.cs
  - src/AgroPlan.Domain/AgroPlan.Domain.csproj
  - src/AgroPlan.Domain/Crops/AgronomicParameter.cs
  - src/AgroPlan.Domain/Crops/CropCatalogEntry.cs
  - src/AgroPlan.Domain/Crops/CropCycle.cs
  - src/AgroPlan.Domain/Crops/CropFramework.cs
  - src/AgroPlan.Domain/Farms/Farm.cs
  - src/AgroPlan.Domain/Farms/FarmMembershipAuditEntry.cs
  - src/AgroPlan.Domain/Farms/Parcel.cs
  - src/AgroPlan.Domain/Logbook/LogbookAttachment.cs
  - src/AgroPlan.Domain/Logbook/LogbookEntry.cs
  - src/AgroPlan.Domain/Planning/OutboxMessage.cs
  - src/AgroPlan.Domain/Planning/PlantingFrameProposal.cs
  - src/AgroPlan.Domain/Planning/SowingProposal.cs
  - src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj
  - src/AgroPlan.Infrastructure/Email/ConfiguredEmailSender.cs
  - src/AgroPlan.Infrastructure/Identity/ApplicationUser.cs
  - src/AgroPlan.Infrastructure/Identity/RecoveryService.cs
  - src/AgroPlan.Infrastructure/Identity/RefreshSession.cs
  - src/AgroPlan.Infrastructure/Identity/RefreshSessionService.cs
  - src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs
  - src/AgroPlan.Infrastructure/Messaging/RabbitMqConnection.cs
  - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
  - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/CropCatalogConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/CropCycleConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/CropFrameworkConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmMembershipAuditEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookAttachmentConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/ParcelConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/PlantingFrameProposalConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/RecoveryAuditEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/RecoveryRequestConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/RefreshSessionConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/SowingProposalConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/CropCatalogRepository.cs
  - src/AgroPlan.Infrastructure/Persistence/FarmAuthorizationService.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/AccountSessionsAndRecovery.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/CropCatalog.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/CropCycles.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/FarmAdministration.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/FarmParcels.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/InitialFarmIdentity.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/Logbook.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/LogbookAttachments.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/PlantingFrameProposal.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/SowingJobsOutbox.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/SowingProposalConfirmation.cs
  - src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs
  - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
  - src/AgroPlan.Worker/AgroPlan.Worker.csproj
  - src/AgroPlan.Worker/Climate/UnconfiguredClimateProvider.cs
  - src/AgroPlan.Worker/Jobs/PlantingFrameConsumer.cs
  - src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs
  - src/AgroPlan.Worker/Program.cs
  - src/AgroPlan.Worker/appsettings.Development.example.json
  - src/AgroPlan.sln
  - src/Directory.Build.props
findings:
  critical: 2
  warning: 1
  info: 0
  total: 3
  necessity: 0
status: issues_found
---

# Phase 1: Code Review Report

**Reviewed:** 2026-11-10T15:00:00Z  
**Depth:** deep  
**Files Reviewed:** 95  
**Status:** issues_found

## Summary

Reviewed all 95 changed source/configuration files at `develop` HEAD `0aede39455d05dce4d387c7f4eaacffbdeda3a8d`. Farm-scoped controller queries consistently constrain resources to the route farm and the authorization handler rechecks active owner/administrator membership. Two blockers remain: finalized attachment objects can still be overwritten with their live signed PUT URL, and the Gemini draft can emit ungrounded agronomic statements when the code knows required climate/agronomic inputs are missing. The async consumer also lacks an atomic claim, so duplicate deliveries across worker replicas can trigger duplicate generation and conflicting writes. No tests or builds were run.

## Critical Issues

### CR-01: BLOCKER — Finalized logbook evidence remains overwriteable

**File:** `src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs:29-35,44-55`; `src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs:30-42`
**Issue:** The returned presigned URL authorizes an unconditional PUT to a stable object key for ten minutes. Finalization only checks metadata and marks the database row; it neither consumes/revokes that URL nor copies the object to an immutable key. A caller can upload valid bytes, finalize, then reuse the still-live URL to replace the bytes while the finalized row and download endpoint continue treating the replacement as immutable evidence. Metadata can also remain identical (same content type and byte length), so the finalize checks do not detect the replacement.
**Fix:** Upload to a one-time staging key, atomically consume the upload authorization at finalize, verify the staged object, then copy it to a distinct immutable final key and issue downloads only for that final key. Alternatively enforce storage-side create-only semantics and bind finalization to an object version/checksum that download verifies.

### CR-02: BLOCKER — Gemini can present invented guidance despite known missing inputs

**File:** `src/AgroPlan.Worker/AI/GeminiProposalProvider.cs:137-150`; `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs:123-155`
**Issue:** The consumer invokes Gemini even when climate and validated crop parameters are absent, then stores and returns its free-form `Summary` as a completed draft. Validation only rejects digits and unknown optional source IDs; it does not verify factual claims or constrain prose to the supplied evidence. Nonnumeric fabricated guidance can therefore pass (for example, claims about seasonal suitability, irrigation, or disease risk). The separately computed `missingInputs` and `sources` do not validate the summary, and the provider's validated `SourceIds`/`MissingInputs` are discarded. This conflicts with the pilot rule to report missing data and not invent parameters.
**Fix:** When required inputs are missing, skip model-generated agronomic prose and return a deterministic “insufficient validated data” response. When sufficient inputs exist, return claims in a structured, evidence-bound form and validate every cited source/claim server-side; retain the validated source IDs and missing-input data in the saved result.

## Warnings

### WR-01: WARNING — Worker deliveries are not atomically claimed for processing

**File:** `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs:94-110,123-160`
**Issue:** A delivery only checks `proposal.Status == Pending`; it does not atomically transition/claim the proposal before the external Gemini call. RabbitMQ can redeliver and multiple worker replicas can process the same pending row concurrently. Both calls may incur provider cost, then race to save different results; `MarkCompleted` only checks the in-memory status and no concurrency token/conditional update prevents the last writer from replacing the first. This weakens idempotency under at-least-once delivery.
**Fix:** Add an atomic database claim/lease or status transition before processing (conditional update from Pending with a concurrency token). Acknowledge duplicate deliveries once another worker owns or completed the job, and make completion conditional on the claim/version.

---

_Reviewed: 2026-11-10T15:00:00Z_  
_Reviewer: the agent (gsd-code-reviewer)_  
_Depth: deep_
