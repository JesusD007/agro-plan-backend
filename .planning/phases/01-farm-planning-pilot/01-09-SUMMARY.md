---
phase: 01-farm-planning-pilot
plan: 09
subsystem: api-documentation-and-ai-adapter
tags: [openapi, scalar, xunit, ai-provider, dependency-injection]
dependency-graph:
  requires: [01-08]
  provides: [provider-neutral-ai-contract, development-scalar-reference]
  affects: [api, worker, unit-tests, integration-tests]
tech-stack:
  added: [Microsoft.AspNetCore.OpenApi 10.0.13, Scalar.AspNetCore 2.19.0]
  patterns: [provider-neutral-application-abstraction, development-only-api-reference, bearer-openapi-transformer]
key-files:
  created:
    - src/AgroPlan.Application/Abstractions/IAiProposalProvider.cs
  modified:
    - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
    - src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs
    - src/AgroPlan.Worker/Program.cs
    - src/AgroPlan.Api/AgroPlan.Api.csproj
    - src/AgroPlan.Api/Program.cs
    - .planning/phases/01-farm-planning-pilot/01-VALIDATION.md
decisions:
  - Keep Gemini as the concrete adapter registered for the provider-neutral IAiProposalProvider contract.
  - Use generic AI provider failure categories in the Worker so the orchestration no longer depends on Gemini exception types.
  - Serve the OpenAPI document and Scalar UI only in Development; include JWT bearer metadata only on authorized operations.
metrics:
  duration: 11 minutes
  completed: 2026-11-10
  commits: 2
  plan_head_before: c397811
  plan_head_after: e792dd7974397fbf66482f129040cbae86b9ce20
status: complete
actuals:
  tokens: 5989
  tasks: 2
  commits: 2
---

# Phase 1 Plan 09: Provider-neutral AI contract and Scalar API reference

The Worker now depends on a provider-neutral AI proposal contract while continuing to use the Gemini adapter, and Development exposes the generated OpenAPI document through Scalar.

## What Changed

- Replaced `IGeminiProposalProvider` with `IAiProposalProvider`; renamed the draft record to `AiProposalDraft` and added provider-neutral failure categories.
- Kept `GeminiProposalProvider` as the registered implementation. The sowing consumer maps generic provider failures to safe, provider-neutral failure statuses.
- Added ASP.NET Core OpenAPI generation and Scalar API reference at `/scalar`, both mapped only in Development. The OpenAPI document includes bearer authentication metadata for protected operations.
- Updated the validation strategy and earlier Phase 1 architecture references to use the generic contract and document the new API reference.

## Verification

- `dotnet build src/AgroPlan.sln -m:1 --no-restore` — passed with 0 warnings and 0 errors using .NET SDK 10.0.403.
- No HTTP smoke test was run in this plan; Scalar/OpenAPI runtime behavior is assigned to the Phase 1 integration test plan (01-11).
- No test suite was run in this plan; unit and integration coverage is being added by plans 01-10 and 01-11.

## Deviations from Plan

- Generalized the provider failure exception boundary as well as the interface and result record. This keeps the consumer’s retry/failure handling independent of the concrete vendor while preserving its safe failure categories.
- The parallel MSBuild invocation returned a failure without diagnostics; a serial `-m:1` build completed successfully and was used for verification.

## Commits

- `7f52910` — `refactor(01-09): neutralize AI proposal contract`
- `e792dd7` — `feat(01-09): add development Scalar API reference`

## Self-Check: PASSED

- Confirmed the new abstraction, API documentation setup, and validation artifact exist.
- Both task commits are ancestors of this plan branch.
- Working tree clean after summary creation.
