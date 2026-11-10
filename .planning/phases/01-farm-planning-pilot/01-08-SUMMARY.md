---
phase: 01-farm-planning-pilot
plan: 08
subsystem: api
tags: [aspnet-core, ef-core, postgres, supabase-storage, s3, sigv4, logbook]
requires:
  - phase: 01-farm-planning-pilot
    provides: Current farm membership authorization, parcels, crop cycles, and PostgreSQL persistence
provides:
  - Farm-scoped append-only logbook with linked correction history
  - Private object storage upload and download authorization, metadata finalization
affects: [farm-operations, logbook, file-storage]
actuals:
  tokens: 8017
  tasks: 2
  commits: 2
tech-stack:
  added: [AWSSDK.S3 4.0.104.11]
  patterns: [Database-enforced immutable logbook entries, Farm-scoped composite foreign keys, Ten-minute SigV4 object URLs]
key-files:
  created:
    - src/AgroPlan.Api/Controllers/LogbookController.cs
    - src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs
    - src/AgroPlan.Domain/Logbook/LogbookEntry.cs
    - src/AgroPlan.Domain/Logbook/LogbookAttachment.cs
    - src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/Logbook.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/LogbookAttachments.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj
    - src/AgroPlan.Api/appsettings.Development.example.json
key-decisions:
  - "Use the official AWS SDK for .NET S3 adapter to create Supabase-compatible SigV4 URLs with a ten-minute expiry."
  - "Keep Supabase S3 credentials server-side and require a private bucket with hard size/lifecycle policies before production."
patterns-established:
  - "Every logbook query and attachment authorization uses the active farm-membership policy and farm-bound resource lookup."
  - "Corrections append a new row linked to the original; PostgreSQL rejects updates and deletes to logbook entries."
requirements-completed: [REQ-pilot-manual-logbook, REQ-rf-g-05]
coverage:
  - id: D1
    description: Farm members can list and append supported activities and create linked corrections without changing the original entry.
    requirement: REQ-pilot-manual-logbook
    verification:
      - kind: other
        ref: dotnet build src/AgroPlan.sln (.NET 10)
        status: pass
    human_judgment: true
    rationale: API authorization and correction scenarios still require later functional acceptance; no tests were run.
  - id: D2
    description: Authorized farm members can receive expiring direct transfer URLs and finalize only matching private objects.
    requirement: REQ-rf-g-05
    verification:
      - kind: other
        ref: dotnet build src/AgroPlan.sln (.NET 10)
        status: pass
    human_judgment: true
    rationale: Live Supabase bucket policy and transfer behavior require production provisioning and functional acceptance.
metrics:
  duration: 50min
  completed: 2026-11-10
  commits: 2
  plan_head_before: 582da214bd7bdefd4e33c9883a08ca6d2f066adc
  plan_head_after: c11a3162464ef1919be5b74c4bb021c4201f7c34
status: complete
---

# Phase 1 Plan 08: Immutable Logbook and Photo Uploads Summary

**The API records farm operations as immutable, linked history and authorizes private Supabase photo transfers through ten-minute S3 Signature V4 URLs.**

## Performance

- **Duration:** 50 min
- **Started:** 2026-11-10T04:15:00Z
- **Completed:** 2026-11-10T05:05:00Z
- **Tasks:** 2
- **Files changed:** 17

## Accomplishments

- Added list and append endpoints for sowing, irrigation, fumigation, and harvest; corrections append a new record linked to the preserved original with actor, timestamp, and reason.
- Enforced same-farm parcel, cycle, and correction references through database foreign keys, plus a PostgreSQL trigger that rejects logbook entry updates and deletes.
- Added farm-authorized direct uploads and downloads for JPEG, PNG, and WebP. Upload URLs last ten minutes and sign the expected content type; finalization checks the entry, expiry, deterministic object key, actual content type, and actual size before storing metadata.

## Task Commits

1. **Task 1: Append immutable daily activity records and linked corrections** - `d9d5925` (feat)
2. **Task 2: Authorize direct photo uploads and finalize farm-owned metadata** - `c11a316` (feat)

## Files Created/Modified

- `src/AgroPlan.Api/Controllers/LogbookController.cs` - Authorized entry list, append, and correction endpoints.
- `src/AgroPlan.Api/Controllers/LogbookAttachmentsController.cs` - Upload authorization, finalization, and download authorization endpoints.
- `src/AgroPlan.Domain/Logbook/LogbookEntry.cs` and `LogbookAttachment.cs` - Domain records for immutable operations and upload metadata.
- `src/AgroPlan.Application/UseCases/AppendLogbookEntry.cs` and `CorrectLogbookEntry.cs` - Entry construction rules.
- `src/AgroPlan.Infrastructure/Storage/SupabaseFileStorage.cs` - S3-compatible Supabase adapter using AWS SDK Signature V4.
- `src/AgroPlan.Infrastructure/Persistence/Configurations/LogbookEntryConfiguration.cs` and `LogbookAttachmentConfiguration.cs` - EF mappings and farm-bound relationships.
- `src/AgroPlan.Infrastructure/Persistence/Migrations/Logbook.cs` and `LogbookAttachments.cs` - Logbook tables, indexes, constraints, and immutable-entry trigger.
- `src/AgroPlan.Api/Program.cs`, `appsettings.Development.example.json`, and `src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj` - DI, example settings, and the verified official AWS SDK package.

## Decisions Made

- Used AWS SDK for .NET's `AWSSDK.S3` 4.0.104.11 for Supabase S3-compatible Signature V4 signing. Its official AWS SDK repository identifies it as the official .NET SDK; NuGet lists the pinned version, and AWS API docs support PUT, content-type, and expiry parameters. Supabase S3 credentials stay in server configuration and are not returned or logged.
- The example configuration names a private bucket. Before production, provision that bucket with a 10 MiB maximum object size and a 24-hour cleanup policy for unfinalized uploads. Supabase S3 access keys can bypass RLS and have broad project storage access, so signing is limited to a server-generated farm/entry/attachment path.

## Deviations from Plan

**1. Used Supabase's S3-compatible endpoint and AWS SDK presigned PUTs.**
- **Found during:** Task 2
- **Issue:** Supabase standard signed upload tokens use a fixed two-hour lifetime, which cannot satisfy D-11's ten-minute expiry.
- **Fix:** Used the official AWS SDK with S3-compatible SigV4 signing, a ten-minute expiration, and a signed content type.
- **Verification:** Final .NET 10 solution build passed with zero warnings and zero errors; no storage resources were provisioned.
- **Committed in:** `b6d906f`

**2. Added farm-authorized signed download URLs.**
- **Found during:** Task 2
- **Issue:** The threat model requires private object reads to be authorized through the farm policy.
- **Fix:** Added a download endpoint for finalized attachments that rechecks active farm membership and issues a ten-minute GET URL.
- **Verification:** Final .NET 10 solution build passed with zero warnings and zero errors.
- **Committed in:** `b6d906f`

**Total deviations:** 2 implementation adjustments. Both close the D-11 expiry and T-01-17 private-read requirements.

## Issues Encountered

- Initial build found a missing `Microsoft.Extensions.Configuration` import in the storage adapter. Added it; the final build passed with zero warnings and zero errors.

## User Setup Required

Live storage requires a configured private Supabase bucket and `SUPABASE_URL`, `SUPABASE_STORAGE_BUCKET`, `S3_ENDPOINT`, `S3_REGION`, `S3_ACCESS_KEY_ID`, and `S3_SECRET_ACCESS_KEY` server-side settings. Configure a 10 MiB bucket upload cap and 24-hour cleanup for abandoned objects before production. No credentials or Supabase resources were created.

## Next Phase Readiness

The API and database contract are ready for the remaining pilot integration. Apply the two included migrations in order and configure the external private bucket policies before enabling photo transfers in production. Functional authorization and upload acceptance scenarios remain for the later verification step.

---
*Phase: 01-farm-planning-pilot*
*Completed: 2026-11-10*

## Self-Check: PASSED

- Both task commits are ancestors of the worktree branch.
- The final .NET 10 solution build passed with zero warnings and zero errors.
- No `.codex/` tooling files were staged or committed.
