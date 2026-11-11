---
phase: 04-applied-farm-intelligence
fixed_at: 2026-11-11T17:10:37Z
review_path: .planning/phases/04-applied-farm-intelligence/04-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
verification_location: main checkout
---

# Phase 04: Code Review Fix Report

**Fixed at:** 2026-11-11T17:10:37Z  
**Source review:** `.planning/phases/04-applied-farm-intelligence/04-REVIEW.md`  
**Iteration:** 1

**Summary:**
- Findings in scope: 5
- Fixed: 5
- Skipped: 0

## Fixed Issues

### CR-01: Advisor receives the unfiltered summary narrative

**Files modified:** `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs`, `src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs`  
**Commit:** `67a7b0e`  
**Applied fix:** Advisor responses omit narrative prose because the narrative is not structured by grant scope. Added tests proving both single-scope grants cannot receive unfiltered prose.
**Verification:** Focused PostgreSQL integration tests passed: 2/2.

### CR-02: Report finalization succeeds after farm membership is revoked

**Files modified:** `src/AgroPlan.Api/Controllers/SymptomReportsController.cs`, `src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs`  
**Commit:** `4a72e04`  
**Applied fix:** Require an active permitted farm role before loading the report and revalidate it inside the transaction before report state and outbox changes. Added revocation regression coverage.
**Verification:** Focused PostgreSQL integration test passed: 1/1.

### CR-03: Requested additional evidence cannot be uploaded

**Files modified:** `src/AgroPlan.Domain/Symptoms/SymptomReport.cs`, `src/AgroPlan.Api/Controllers/SymptomReportsController.cs`, `src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs`  
**Commit:** `41ceb84`  
**Applied fix:** Added a guarded transition from pending to upload only after the latest agronomist decision requests evidence. The existing upload authorization then appends photos, and re-finalization returns the report to pending while preserving earlier photos and decisions.
**Verification:** End-to-end PostgreSQL integration test passed: 1/1.

### WR-01: Retry overcounts pixels for photos already finalized

**Files modified:** `src/AgroPlan.Domain/Symptoms/SymptomReport.cs`, `src/AgroPlan.Api/Controllers/SymptomReportsController.cs`, `src/AgroPlan.Infrastructure/Persistence/Configurations/SymptomReportConfiguration.cs`, `src/AgroPlan.Infrastructure/Persistence/Migrations/SymptomPhotoDimensions.cs`, `src/AgroPlan.UnitTests/Symptoms/SymptomReportTests.cs`, `src/AgroPlan.IntegrationTests/Worker/SymptomReportCleanupTests.cs`, `src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs`  
**Commit:** `7465392`  
**Applied fix:** Persist inspected width and height, include them in idempotent finalization, and use stored dimensions when summing finalized photos during retries. Added a regression with 45 million pixels already persisted and a new image.
**Verification:** Focused PostgreSQL integration test passed: 1/1.

### WR-02: API never enqueues weekly-summary or symptom AI work

**Files modified:** `src/AgroPlan.Application/Abstractions/IAppliedFarmIntelligenceActivation.cs`, `src/AgroPlan.Infrastructure/AI/ConfigurationAppliedFarmIntelligenceActivation.cs`, `src/AgroPlan.Api/Program.cs`, `src/AgroPlan.Api/Controllers/WeeklySummariesController.cs`, `src/AgroPlan.Api/Controllers/SymptomReportsController.cs`, `src/AgroPlan.Worker/AppliedFarmIntelligenceWorkerRegistration.cs`, `src/AgroPlan.IntegrationTests/IntegrationSupport.cs`, `src/AgroPlan.IntegrationTests/Api/WeeklySummaryTests.cs`, `src/AgroPlan.IntegrationTests/Api/SymptomReportIntegrationTests.cs`  
**Commit:** `8dac2d9`  
**Applied fix:** Added a shared activation contract and configuration predicate requiring feature enabled, named provider, exact verified operation, and approved evidence. API outbox creation now consults this contract without resolving a provider; Worker uses the same predicate in addition to explicit provider registration. Added enabled/disabled outbox and API/Worker composition tests for summaries, plus symptom API/Worker composition coverage.
**Verification:** Focused API/Worker PostgreSQL integration tests passed: 2/2.

## Full Suite

Ran from the main checkout using .NET 10:

```text
dotnet test src/AgroPlan.sln --no-restore -m:1 -nr:false --logger 'console;verbosity=minimal'
UnitTests: 153 passed, 0 failed, 0 skipped
IntegrationTests: 92 passed, 0 failed, 0 skipped
Total: 245 passed, 0 failed, 0 skipped
```

Full output is saved in `%TEMP%\agroplan-phase4-full-test.log`. The build reported existing xUnit analyzer warnings (`xUnit1051` and `xUnit2031`); the suite passed.

---

_Fixed: 2026-11-11T17:10:37Z_  
_Fixer: the agent (gsd-code-fixer)_  
_Iteration: 1_
