---
phase: "02"
slug: "farm-operations-and-safety"
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-10"
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for operational records, safety decisions, reporting, advisor access, and schedule assistance.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 |
| **Config file** | `src/AgroPlan.UnitTests/xunit.runner.json`; `src/AgroPlan.IntegrationTests/xunit.runner.json` |
| **Quick run command** | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1` |
| **Full suite command** | `dotnet test src/AgroPlan.sln --no-restore -m:1` |
| **Estimated runtime** | ~60 seconds with PostgreSQL and RabbitMQ Testcontainers available |

---

## Sampling Rate

- **After every task commit:** Run the task's focused unit or integration test command below.
- **After every plan wave:** Run `dotnet test src/AgroPlan.sln --no-restore -m:1`.
- **Before `$gsd-verify-work`:** Full suite must be green.
- **Max feedback latency:** 60 seconds for unit tests; integration tests depend on container startup.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | REQ-rf-g-02 | T-02-01, T-02-02 | Farm-scoped append-only records and linked corrections | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `ExpenseEntriesTests.cs` ✅ | ✅ green |
| 02-01-02 | 01 | 1 | REQ-rf-g-02 | T-02-02 | Preserve source values and reject invalid currency/unit data | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `ExpenseEntryTests.cs` ✅ | ✅ green |
| 02-02-01 | 02 | 2 | REQ-rf-g-31 | T-02-04 | Farm-bound irrigation records, source units, immutable correction history | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `IrrigationEntriesTests.cs` ✅ | ✅ green |
| 02-02-02 | 02 | 2 | REQ-rf-g-32 | T-02-05 | Application frequency is sourced when exact rules are current; otherwise unavailable | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `InputApplicationEntryTests.cs` ✅ | ✅ green |
| 02-03-01 | 03 | 2 | REQ-rf-g-35 | T-02-06 | Weather response carries freshness/source state; missing data is not treated as safe weather | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `AdvisoriesTests.cs` ✅ | ✅ green |
| 02-03-02 | 03 | 2 | REQ-rf-g-08 | T-02-07 | No price is fabricated when a provider/source is not configured | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `AdvisoriesTests.cs` ✅ | ✅ green |
| 02-04-01 | 04 | 3 | REQ-rf-g-07 | T-02-08 | Exact current product/crop/use PHI evidence gates eligibility; unknown blocks | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `HarvestEligibilityEvaluatorTests.cs` ✅ | ✅ green |
| 02-04-02 | 04 | 3 | REQ-rf-g-42 | T-02-09 | Blocked harvest is not persisted; allowed records retain safety provenance | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `HarvestSafetyTests.cs` ✅ | ✅ green |
| 02-05-01 | 05 | 4 | REQ-rf-g-04, REQ-rf-g-33, REQ-rf-g-34, REQ-rf-g-42 | T-02-10 | Validated calendar rules and progress; harvest windows/actions are sourced or explicitly unavailable | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `CropCalendarEvaluatorTests.cs` ✅ | ✅ green |
| 02-05-02 | 05 | 4 | REQ-rf-g-04, REQ-rf-g-33, REQ-rf-g-34, REQ-rf-g-42 | T-02-10 | Farm-scoped calendar outputs and missing-input states preserve separate PHI gate | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `CropCalendarTests.cs` ✅ | ✅ green |
| 02-06-01 | 06 | 4 | REQ-rf-g-03, REQ-rf-g-15, REQ-rf-g-36 | T-02-13 | Costs/yields retain currency and units; unsupported conversions are not computed | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `OperationsReportTests.cs` ✅ | ✅ green |
| 02-06-02 | 06 | 4 | REQ-rf-g-03, REQ-rf-g-15, REQ-rf-g-36 | T-02-12, T-02-13 | Scoped JSON/CSV outputs retain provenance, correction status, and safe CSV escaping | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `OperationsReportsTests.cs` ✅ | ✅ green |
| 02-07-01 | 07 | 5 | REQ-rf-g-37 | T-02-14 | Grants are bounded, read-only, farm/category scoped and revocable | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `AdvisorGrantTests.cs` ✅ | ✅ green |
| 02-07-02 | 07 | 5 | REQ-rf-g-37 | T-02-14, T-02-15 | Current grant is checked per request; same-token revocation and CSV scopes are enforced | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Api"` | `AdvisorAccessTests.cs` ✅ | ✅ green |
| 02-08-01 | 08 | 5 | REQ-rf-g-13, REQ-rf-g-42 | T-02-16 | Deterministic proposals require explicit producer confirmation and durable idempotent delivery | integration | `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~Worker"` | `OutboxAndConsumerTests.cs` ✅ | ✅ green |
| 02-08-02 | 08 | 5 | REQ-rf-g-13 | T-02-16, T-02-17 | Provider-neutral Gemini explanation is grounded, non-authoritative, and rejects unsupported/injected output | unit | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj` | `CalendarExplanationValidationTests.cs` ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing xUnit v3 infrastructure and Testcontainers cover all Phase 02 requirements; no Wave 0 test scaffolding was needed.

---

## Manual-Only Verifications

All implemented behaviors have automated verification. Production activation remains gated on choosing and validating climate, market-price, and agronomic evidence sources, and on satisfying the AI evaluation/operations gates in `02-AI-SPEC.md`; these are product/data prerequisites, not untested implementation paths. No live Gemini or climate/price provider call was made during tests.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s for unit tests; integration timing varies with containers
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-11-10
