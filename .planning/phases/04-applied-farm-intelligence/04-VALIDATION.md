---
phase: "04"
slug: "applied-farm-intelligence"
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-11"
---

# Phase 04 — Validation Strategy

> Per-phase validation contract. The implementation plans must replace provisional plan/task labels below after decomposition.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (existing repository test framework) |
| **Config file** | `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj`, `src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1 -nr:false` |
| **Full suite command** | `dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false` |
| **Estimated runtime** | 3–5 minutes with Docker/Testcontainers available |

Use the installed .NET 10 SDK. On this workstation, the SDK is installed at `C:/Users/romer/.dotnet/dotnet.exe`; integration tests require a reachable Docker engine. Pass cancellation tokens to xUnit v3 async tests and keep provider/network calls disabled in ordinary CI.

## Sampling Rate

- **After every task commit:** Run the focused xUnit v3 test(s) for the changed behavior and affected API/Worker integration suite when persistence, authorization, or jobs change.
- **After every plan wave:** Run `dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false`.
- **Before phase verification:** Full solution suite must pass; live provider evaluation is a separate opt-in gate and cannot replace deterministic tests.
- **Max feedback latency:** 5 minutes for the current complete backend suite.

## Per-Requirement Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 04-01 Task 1: Voice draft tracer | 04-01 | 1 | REQ-rf-g-39 | T-04-01–T-04-04 | Both language choices, five-minute cap, assigned-task authorization, partial/uncertain fields, no logbook append before explicit confirmation, one idempotent append | xUnit v3 unit + PostgreSQL/API/Worker/OpenAPI integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~VoiceDraftIntegrationTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-01 Task 2: Voice audio expiry | 04-01 | 1 | REQ-rf-g-39 | T-04-03–T-04-04 | Delete on confirmation or seven days after processing; retry storage failures without losing the confirmed logbook row | xUnit v3 PostgreSQL/Worker integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~VoiceDraftCleanupTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-02 Task 1: Weekly summary tracer | 04-02 | 1 | REQ-rf-g-40 | T-04-05–T-04-08 | Monday–Sunday farm-local window, confirmed source records, pending tasks, missing markers, deterministic totals/source IDs, cycle filter | xUnit v3 unit + PostgreSQL/API/OpenAPI integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~WeeklySummaryTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-02 Task 2: Summary prose and advisor scope | 04-02 | 1 | REQ-rf-g-40 | T-04-05–T-04-08 | Provider output cannot alter totals/sources; share and source read recheck active grant/scope; task details without a grant scope are omitted | xUnit v3 PostgreSQL/API/Worker integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~WeeklySummaryConsumerTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-03 Task 1: Symptom observation tracer | 04-03 | 2 | REQ-rf-g-43 | T-04-09–T-04-13 | Private photo intake, visible signs only, no AI diagnosis/treatment, insufficient evidence remains pending | xUnit v3 unit + PostgreSQL/API/Worker/OpenAPI integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReportIntegrationTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-03 Task 2: Agronomist review/photo deletion | 04-03 | 2 | REQ-rf-g-43 | T-04-09–T-04-13 | Trusted verification plus current farm authorization, denial after revocation, pending evidence requests, delete photos on close/reject and retain report | xUnit v3 PostgreSQL/API/Worker integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SymptomReportCleanupTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-04 Task 1: Provider-gated Worker registration | 04-04 | 3 | REQ-rf-g-39,40,43 | T-04-14–T-04-17 | Consumers remain disabled by default; speech requires both locales and approval evidence; fake/unconfigured providers stay in development/test | xUnit v3 Worker activation integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AppliedFarmIntelligenceWorkerActivationTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-04 Task 2: Cross-feature API/privacy regression | 04-04 | 3 | REQ-rf-g-39,40,43 | T-04-14–T-04-17 | Routes match Scalar/OpenAPI coverage; Production docs/provider calls remain disabled; permissions/media deletion and log redaction hold across features | xUnit v3 API/Worker integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AppliedFarmIntelligenceContractTests|FullyQualifiedName~AppliedFarmIntelligenceWorkerActivationTests" -m:1 -nr:false` | ❌ New | ⬜ pending |
| 04-04 Task 3: AI-SPEC reference fixtures | 04-04 | 3 | REQ-rf-g-39,40,43 | T-04-14–T-04-17 | 45 fictional offline reference cases span both languages, code-switching, farm-week summaries, and symptom abstention without network calls | xUnit v3 Category=AiEval | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore --filter "Category=AiEval" -m:1 -nr:false` | ❌ New | ⬜ pending |

## Wave 0 Requirements

- [ ] Provider-neutral speech, summary, and symptom-description contracts with deterministic fake providers and strict response validation.
- [ ] PostgreSQL integration coverage for job/report/draft states, outbox idempotency, current membership/task/grant checks, and farm-local reporting bounds.
- [ ] Storage deletion and retry coverage for confirmed/expired voice recordings and closed/rejected symptom photos.
- [ ] Provider-specific live tests stay opt-in; create the reviewed, consented Dominican Spanish/Haitian Creole field-audio corpus before enabling either language for production. Provider selection and language evaluation are activation gates, not substitutes for xUnit tests.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Whether real Haitian Creole/Spanish field audio meets the provider quality threshold | REQ-rf-g-39 | Requires representative, consented bilingual field recordings and human reference transcripts | Use a private, versioned evaluation set; speakers verify numbers, units, negation, activity labels, and code-switch spans; keep speech disabled until each language meets the AI-SPEC thresholds. |
| Agronomic usefulness of visual observations and requests for additional photos | REQ-rf-g-43 | Domain experts must judge visible-sign descriptions and adequacy of requested evidence | Dominican agronomists review consented, private crop-photo cases and confirm no pre-review diagnosis/treatment. |

## Validation Sign-Off

- [ ] All tasks have `<automated>` verification or an explicit Wave 0 dependency.
- [ ] Sampling continuity: no three consecutive tasks without automated verification.
- [ ] Wave 0 covers all missing test references.
- [ ] No watch-mode flags.
- [ ] `nyquist_compliant: true` set in frontmatter after execution and verification.

**Approval:** pending planning and implementation
