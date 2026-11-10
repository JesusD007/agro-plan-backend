---
phase: 01-farm-planning-pilot
fixed_at: 2026-11-10T05:43:27Z
review_path: .planning/phases/01-farm-planning-pilot/01-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 1: Code Review Fix Report

**Fixed at:** 2026-11-10T05:43:27Z  
**Source review:** `.planning/phases/01-farm-planning-pilot/01-REVIEW.md`  
**Iteration:** 1

**Summary:**
- Findings in scope: 3
- Fixed: 3
- Skipped: 0

## Verification

Source sections were re-read after each fix in the isolated `gsd-reviewfix/01-20261110` worktree. No tests or builds were run, as requested; the project owner will run the final compile after merging.

## Fixed Issues

### CR-01: Finalized logbook evidence remains overwriteable

**Files modified:** `src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs`, `src/AgroPlan.Application/Abstractions/IFileStorage.cs`, `src/AgroPlan.Domain/Logbook/LogbookAttachment.cs`, `src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookAttachmentConfiguration.cs`, `src/AgroPlan.Infrastructure/Persistence/Migrations/ImmutableLogbookAttachmentStorage.cs`, `src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs`
**Commits:** `e8d9236`, `c397811`
**Applied fix:** Upload URLs now target a staging key. Finalization claims the row, validates the upload, copies it to a distinct final key, and removes the staging object. Retries use an existing final object when present, and finalized rows return successfully without copying again. Downloads use only the final key. The migration preserves existing storage keys; on the first download, legacy finalized attachments are copied to an immutable key under a database claim before a download URL is issued.

### CR-02: Gemini can present invented guidance despite known missing inputs

**Files modified:** `src/AgroPlan.Application/Abstractions/IGeminiProposalProvider.cs`, `src/AgroPlan.Worker/AI/GeminiProposalProvider.cs`, `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs`
**Commit:** `b901118`
**Applied fix:** The consumer now returns a deterministic insufficient-data result without calling Gemini whenever required climate, validated crop, location, or parcel geometry inputs are missing. When the provider is called, the validated summary, source IDs, missing-input IDs, provider/model, prompt and schema versions, and generation time are stored in `ResultJson`.

### WR-01: Worker deliveries are not atomically claimed for processing

**Files modified:** `src/AgroPlan.Domain/Planning/SowingProposal.cs`, `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs`
**Commit:** `b0d381c`
**Applied fix:** Before reading proposal data or invoking a provider, the consumer conditionally changes `Pending` to `Processing` and increments the concurrency version in one database update. Recent duplicate deliveries therefore ACK without a second provider call. Stale claims can be reclaimed; graceful cancellation releases a claim back to `Pending`, and processing failures are saved as visible `Failed` status. **Fixed: requires human verification** of the lease and retry policy under concurrent delivery and abrupt worker termination.

---

_Fixed: 2026-11-10T05:43:27Z_  
_Fixer: the agent (gsd-code-fixer)_  
_Iteration: 1_
