---
phase: "04"
slug: "applied-farm-intelligence"
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-11"
---

# Phase 04 — Validation Strategy

> Per-phase validation contract. The implementation plans must replace provisional plan/task labels below after decomposition.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (existing repository test framework) |
| **Config file** | `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj`, `src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj` |
| **Quick run command** | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1 -nr:false` |
| **Full suite command** | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.sln --no-restore -m:1 -nr:false` |
| **Estimated runtime** | 3–5 minutes with Docker/Testcontainers available |

Use the installed .NET 10 SDK. On this workstation, the SDK is installed at `C:/Users/romer/.dotnet/dotnet.exe`; integration tests require a reachable Docker engine. Pass cancellation tokens to xUnit v3 async tests and keep provider/network calls disabled in ordinary CI.

## Sampling Rate

- **After every task commit:** Run the focused xUnit v3 test(s) for the changed behavior and affected API/Worker integration suite when persistence, authorization, or jobs change.
- **After every plan wave:** Run the full `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.sln --no-restore -m:1 -nr:false` suite after Wave 1 (04-01/04-02), after Wave 2 (04-03), and after Wave 3 (04-04).
- **Before phase verification:** Run that same full solution suite again immediately before `$gsd-verify-phase`; live provider evaluation is a separate opt-in gate and cannot replace deterministic tests.
- **Max feedback latency:** 5 minutes for the current complete backend suite.

## Per-Requirement Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 04-01 Task 1: Voice draft tracer | 04-01 | 1 | REQ-rf-g-39 | T-04-01–T-04-04 | Per-audio consent, 5-minute/10-MiB limits, current assignment checks before media/provider access and publication, schema/language/semantic provider response validation, uncertainty and explicit confirmation | xUnit v3 unit + PostgreSQL/API/Worker/OpenAPI integration | Unit: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "FullyQualifiedName~VoiceActivityDraftTests" -m:1 -nr:false`; integration: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~VoiceDraftIntegrationTests" -m:1 -nr:false` | ✅ `VoiceActivityDraftTests.cs`, `VoiceDraftIntegrationTests.cs` | ✅ green |
| 04-01 Task 2: Voice audio expiry | 04-01 | 1 | REQ-rf-g-39 | T-04-03–T-04-04 | Delete on confirmation or seven days after processing; retry storage failures without losing the confirmed logbook row | xUnit v3 PostgreSQL/Worker integration | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~VoiceDraftIntegrationTests" -m:1 -nr:false` | ✅ `VoiceDraftIntegrationTests.Confirmed_audio_is_deleted_and_audit_record_remains` | ✅ green |
| 04-02 Task 1: Weekly summary tracer | 04-02 | 1 | REQ-rf-g-40 | T-04-05–T-04-08 | Monday–Sunday farm-local window, confirmed source records, pending tasks, missing markers, deterministic totals/source IDs, cycle filter | xUnit v3 unit + PostgreSQL/API/OpenAPI integration | Unit: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "FullyQualifiedName~WeeklySummaryProjectionTests" -m:1 -nr:false`; integration: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~WeeklySummaryTests" -m:1 -nr:false` | ✅ `WeeklySummaryProjectionTests.cs`, `WeeklySummaryTests.cs` | ✅ green |
| 04-02 Task 2: Summary prose and advisor scope | 04-02 | 1 | REQ-rf-g-40 | T-04-05–T-04-08 | Opaque per-job aliases instead of DB IDs, 64-KiB/16-KiB provider bounds, current membership/grants, bounded retries and stale-event rejection | xUnit v3 unit + PostgreSQL/API/Worker integration | Unit: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "FullyQualifiedName~WeeklySummaryRetryTests" -m:1 -nr:false`; integration: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~WeeklySummaryConsumerTests|FullyQualifiedName~WeeklySummaryTests" -m:1 -nr:false` | ✅ `WeeklySummaryRetryTests.cs`, `WeeklySummaryConsumerTests.cs`, `WeeklySummaryTests.cs` | ✅ green |
| 04-03 Task 1: Symptom observation tracer | 04-03 | 2 | REQ-rf-g-43 | T-04-09–T-04-13 | Per-photo consent, 5-photo/10-MiB/20-MP/50-MP limits, live authorization before retrieval/provider call and before publication, manual path without consent | xUnit v3 unit + PostgreSQL/API/Worker/OpenAPI integration | Unit: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReportTests" -m:1 -nr:false`; integration: `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReportIntegrationTests|FullyQualifiedName~SymptomReportCleanupTests" -m:1 -nr:false` | ✅ `SymptomReportTests.cs`, `SymptomReportIntegrationTests.cs`, `SymptomReportCleanupTests.cs` (consent, worker, retry and manual-fallback cases) | ✅ green |
| 04-03 Task 2: Agronomist review/photo deletion | 04-03 | 2 | REQ-rf-g-43 | T-04-09–T-04-13 | Trusted verification plus current farm authorization, denial after revocation, pending evidence requests, delete photos on close/reject and retain report | xUnit v3 PostgreSQL/API/Worker integration | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReportIntegrationTests|FullyQualifiedName~SymptomReportCleanupTests" -m:1 -nr:false` | ✅ `SymptomReportIntegrationTests.Only_currently_verified_and_assigned_agronomist_can_review_and_read_private_photos`, `SymptomReportCleanupTests.Rejected_report_deletes_private_photo_and_retains_report_and_review_history` | ✅ green |
| 04-04 Task 1: Provider-gated Worker registration | 04-04 | 3 | REQ-rf-g-39,40,43 | T-04-14–T-04-17 | Default-off providers, per-object consent and current authorization gates; speech requires Haitian Creole verification, 20 consented recordings/language and >=95% field accuracy/language | xUnit v3 Worker/API activation integration | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AppliedFarmIntelligenceWorkerActivationTests|FullyQualifiedName~Approved_shared_activation|FullyQualifiedName~Approved_symptom_activation" -m:1 -nr:false` | ✅ `AppliedFarmIntelligenceWorkerActivationTests.cs`, `WeeklySummaryTests.Approved_shared_activation...`, `SymptomReportIntegrationTests.Approved_symptom_activation...` | ✅ green |
| 04-04 Task 2: Cross-feature API/privacy regression | 04-04 | 3 | REQ-rf-g-39,40,43 | T-04-14–T-04-17 | Routes match Scalar/OpenAPI coverage; Production docs/provider calls remain disabled; permissions/media deletion and log redaction hold across features | xUnit v3 API/Worker integration | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AppliedFarmIntelligenceContractTests|FullyQualifiedName~AppliedFarmIntelligenceWorkerActivationTests" -m:1 -nr:false` | ✅ `AppliedFarmIntelligenceContractTests.cs`, OpenAPI tests for voice/summary/symptom | ✅ green |
| 04-04 Task 3: AI-SPEC reference fixtures | 04-04 | 3 | REQ-rf-g-39,40,43 | T-04-14–T-04-17 | 45 fictional offline reference cases span both languages, code-switching, farm-week summaries, and symptom abstention without network calls | xUnit v3 Category=AiEval | `& 'C:/Users/romer/.dotnet/dotnet.exe' test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "Category=AiEval" -m:1 -nr:false` | ✅ `Phase04EvaluationCases.cs`, `Phase04EvaluationTests.cs` (45 fixture records) | ✅ green |

## Wave 0 Requirements

- [x] Provider-neutral speech, summary, and symptom-description contracts with deterministic fake providers and strict response validation.
- [x] PostgreSQL integration coverage for job/report/draft states, outbox idempotency, current membership/task/grant checks, and farm-local reporting bounds.
- [x] Storage deletion and retry coverage for confirmed/expired voice recordings and closed/rejected symptom photos.
- [x] Provider-specific live tests stay opt-in; the reviewed, consented Dominican Spanish/Haitian Creole field-audio corpus remains a production activation gate and is not replaced by xUnit tests.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Whether selected provider supports the exact Haitian Creole transcription operation and reaches >=95% exact field accuracy per language | REQ-rf-g-39 | Requires at least 20 consented representative recordings per language, consented Spanish–Kreyòl code-switch cases, and human reference transcripts | Keep both speech languages disabled until the selected provider is verified; separately report exact field accuracy for Dominican Spanish and Haitian Creole. The 45-case baseline is 25 voice (10 Spanish, 10 Creole, 5 code-switch), 10 summary, and 10 symptom cases; synthetic `AiEval` fixtures do not establish provider quality. |
| Privacy/legal review for the selected provider and production media-processing notice/basis | REQ-rf-g-39,40,43 | Depends on the actual vendor terms, data residency, and lawful basis for external audio/photo processing | Complete privacy/legal review before enabling any external speech, summary, or photo provider. Keep operations disabled until approved. |
| Agronomic usefulness of visual observations and requests for additional photos | REQ-rf-g-43 | Domain experts must judge visible-sign descriptions and adequacy of requested evidence | Dominican agronomists review consented, private crop-photo cases and confirm no pre-review diagnosis/treatment. |
| Trusted agronomist identity provisioning | REQ-rf-g-43 | Identity verification is intentionally provided by a trusted process outside this API phase | Provision and verify agronomist identity through the approved trusted process before granting farm-specific review authorization; the API fails closed without it. |

## Validation Sign-Off

- [x] All tasks have automated verification or an explicit manual activation gate.
- [x] Sampling continuity: all tasks have automated tests.
- [x] Wave 0 test references exist and map to green tests.
- [x] No watch-mode flags.
- [x] `nyquist_compliant: true` set in frontmatter after execution and verification.

**Validation result:** All nine plan tasks have existing automated xUnit v3 coverage and were included in the final full-suite run. The final verification suite passed **254/254** (157 unit, 97 integration), with Docker/Testcontainers active. Provider quality, privacy/legal approval, and trusted agronomist provisioning remain explicit production-only manual gates.

## Validation Audit 2026-11-11

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
