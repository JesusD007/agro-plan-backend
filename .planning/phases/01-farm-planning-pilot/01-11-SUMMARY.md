---
phase: 01-farm-planning-pilot
plan: 11
subsystem: testing
tags: [xunit-v3, testcontainers, postgres, rabbitmq, scalar, openapi]
dependency-graph:
  requires: [01-09, 01-10]
  provides: [api-integration-suite, postgres-rabbitmq-fixtures, api-docs-smoke-tests]
  affects: [AgroPlan.Api, AgroPlan.Worker, AgroPlan.sln]
tech-stack:
  added: [xunit.v3, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql, Testcontainers.RabbitMq]
  patterns: [fake-provider integration hosting, bounded Docker preflight, web-default outbox deserialization]
key-files:
  created:
    - src/AgroPlan.IntegrationTests/IntegrationSupport.cs
    - src/AgroPlan.IntegrationTests/Api/IdentityAndFarmAuthorizationTests.cs
    - src/AgroPlan.IntegrationTests/Api/CropCycleAndPlanningTests.cs
    - src/AgroPlan.IntegrationTests/Api/LogbookAndAttachmentsTests.cs
    - src/AgroPlan.IntegrationTests/Api/OpenApiDocumentationTests.cs
    - src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs
  modified:
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs
    - src/AgroPlan.sln
    - .planning/phases/01-farm-planning-pilot/01-VALIDATION.md
decisions:
  - Container-dependent fixtures check Docker first and dynamically skip with a clear reason if no engine responds.
  - API docs-only tests remove hosted background services so they do not depend on PostgreSQL or RabbitMQ.
  - Keep Nyquist sign-off false until the full suite runs successfully on a Docker-enabled runner.
metrics:
  duration: 2h
  completed: 2026-11-10
  commits: 3
  plan_head_before: dca0fe7bf273a7982080190036b6c2f640dda962
  plan_head_after: 4aca5e7915a0dd88135072dce03d1eb1be594b87
status: complete
actuals:
  tokens: 17000
  tasks: 3
  commits: 3
---

# Phase 1 Plan 11: Integration Test Suite Summary

Added eight xUnit v3 HTTP and persistence integration scenarios with PostgreSQL/RabbitMQ Testcontainers fixtures, covering farm authorization and revocation, support-mediated recovery, crop safe mode, immutable logbook corrections, attachment finalization, asynchronous outbox/worker behavior, and Development Scalar/OpenAPI routes.

## Completed Tasks

| Task | Result | Commit |
|---|---|---|
| 1. Farm-scoped API and documentation tests | Added API test host, fixture support, auth/recovery/crop/doc tests, and solution registration. | `7fdb129` |
| 2. Immutable history and worker flow tests | Added logbook/attachment/outbox-worker tests; corrected camel-case event deserialization. | `7242705` |
| 2a. OpenAPI test host correction | Preserved TestHost services while disabling only API background jobs; bounded the docs request timeout. | `4aca5e7` |
| 3. Validation matrix | Documented commands, service requirements, coverage, and remaining sign-off gates. | Included in final plan docs commit |

## Verification

- `dotnet build src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1` — passed, 0 errors; emitted 51 xUnit1051 cancellation-token analyzer warnings.
- `dotnet test ... -m:1` built and launched VSTest, but this runner did not produce a test summary. The Docker Desktop engine pipe was inaccessible (`docker version` returned access denied), and even the filtered OpenAPI-only run stalled before reporting. Test runs were interrupted to avoid a long wait.
- Docker-dependent tests therefore have no observed pass/skip counts from this environment. Run the documented integration and solution commands on a Docker-enabled runner before declaring validation sign-off.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Parse outbox events using web JSON defaults**
- **Found during:** Task 2
- **Issue:** API outbox payloads use camel-case keys while the worker deserialized with default case-sensitive options, leaving required event fields unset.
- **Fix:** Configured `JsonSerializerDefaults.Web` for the sowing consumer event payload.
- **Files modified:** `src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs`
- **Commit:** `7242705`

### Deferred Verification

- Duplicate/concurrent RabbitMQ deliveries and atomic worker claim behavior (WR-01) are not yet covered by an integration assertion.
- The support-mediated recovery, database trigger, and worker integration cases are implemented but could not be behaviorally verified in this runner.

## Self-Check: PASSED

- All eight `[Fact]` tests and the integration project files are present.
- Both implementation commits are ancestors of the plan branch head.
- Nyquist compliance remains false in `01-VALIDATION.md` pending a full green run.
