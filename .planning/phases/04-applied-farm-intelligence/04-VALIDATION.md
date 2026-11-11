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
| Pending plan assignment | TBD | TBD | REQ-rf-g-39 | tenant scope / media retention | Five-minute cap, assigned-task authorization, partial/uncertain fields, explicit confirmation before logbook append, idempotent confirmation, audio deletion at confirmation/deadline | unit + API/PostgreSQL/Worker integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Voice" -m:1 -nr:false` | ❌ New | ⬜ pending |
| Pending plan assignment | TBD | TBD | REQ-rf-g-40 | report scope / advisor access | Monday–Sunday farm-local period, confirmed records + pending tasks + missing markers, deterministic totals/source IDs, cycle filtering, current advisor grant checked on share and each source read | unit + API/PostgreSQL integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~WeeklySummary" -m:1 -nr:false` | ❌ New | ⬜ pending |
| Pending plan assignment | TBD | TBD | REQ-rf-g-43 | agronomist authority / media retention | Visible signs only before review, insufficient evidence stays pending, only verified farm-authorized agronomist can confirm, source photos deleted on close/rejection while report remains | unit + API/PostgreSQL/storage integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~SymptomReport" -m:1 -nr:false` | ❌ New | ⬜ pending |

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
