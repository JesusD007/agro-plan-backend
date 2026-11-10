---
phase: 01-farm-planning-pilot
plan: 01
subsystem: identity-and-farm-boundary
tags: [aspnet-core, identity, jwt, postgresql, ef-core, farm-authorization]
dependency-graph:
  requires: []
  provides: [api-worker-solution, identity-registration-login, farm-membership-authorization]
  affects: [01-02, 01-03, 01-04, 01-05, 01-06, 01-07, 01-08]
tech-stack:
  added: [.NET 10, ASP.NET Core Identity, EF Core, Npgsql]
  patterns: [short-lived-signed-jwt, per-request-membership-check, tenant-filtered-query, transactional-farm-owner-membership]
key-files:
  created:
    - src/AgroPlan.sln
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Api/Identity/IdentityEndpoints.cs
    - src/AgroPlan.Api/Identity/TokenIssuer.cs
    - src/AgroPlan.Api/Authorization/FarmMembershipAuthorizationHandler.cs
    - src/AgroPlan.Api/Controllers/FarmsController.cs
    - src/AgroPlan.Domain/Farms/Farm.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/InitialFarmIdentity.cs
    - Dockerfile.api
    - Dockerfile.worker
  modified: []
decisions:
  - First-party 15-minute HS256 JWTs are validated by the API; signing material is server-side and required outside Development.
  - Farm-scoped reads check active owner/administrator membership in PostgreSQL per request and again in the data query.
  - Farm creation persists the farm and owner membership in one transaction.
metrics:
  duration: 13m
  completed_date: 2026-11-10
  commits: 2
  plan_head_before: 9a641afd842a0dc568e6c9ffe9af54893992baf6
  plan_head_after: 37a8b22c26a4e44834464e1a7bf4be58910d0b47
actuals:
  tokens: 9078
  tasks: 1
  commits: 2
status: blocked
blocked_reason: .NET SDK 10 is not installed; compile-only verification is required by the tracer gate and the session forbids installing tools or accessing the network.
---

# Phase 1 Plan 1: Identity and Farm Boundary Summary

Implemented the .NET solution skeleton and the first persisted account-to-farm path. Registration and sign-in issue short-lived signed JWTs; farm reads check the current database membership and constrain the query to the same active farm role.

## Progress

| Plan task | Status | Commit |
|---|---|---|
| Task 1: .NET hosts, Identity, farm persistence and request-time authorization | Implemented; compile verification blocked | `a859ae9` |
| Task 2: Separate API and Worker container packaging | Not started; the plan is blocked at the Task 1 tracer gate | — |

The plan is **not complete**. The `dotnet build src/AgroPlan.sln` compile-only check has not run because the host has SDK 8.0.204 while the solution targets .NET 10. No tests were added or run, and no manual acceptance scenarios were executed.

## Security and Persistence Notes

- Account registration uses ASP.NET Core Identity password hashing, unique email validation, password policy, lockout and IP-partitioned rate limiting.
- JWT signing uses a server-side setting of at least 32 bytes; Development falls back to an ephemeral process key, while other environments fail startup without the configured secret.
- Farm creation commits the farm and owner membership together. Reads require current active owner/administrator membership and repeat that condition in the PostgreSQL query.
- The initial SQL migration creates the Identity and farm tables in separate `identity` and `agro` schemas. It has not been applied to a database.
- NuGet versions follow the implementation research snapshot; package restore and .NET 10 compatibility remain unverified in this environment.

## Known Stubs

| File | Reason |
|---|---|
| `src/AgroPlan.Worker/Program.cs` | The Worker host remains idle until the later async processing plans add consumers. |

## Blockers and Deferred Verification

- Install or otherwise provide .NET SDK 10, then run `dotnet build src/AgroPlan.sln` before continuing past the tracer gate.
- After the build passes, run the separate Docker packaging task and review both image entry points before continuing with plan 01-02.
- Registration, sign-in, farm creation, cross-farm denial and membership revocation scenarios remain unexecuted acceptance criteria.

## Self-Check: PASSED

- Task 1 and Task 2 commit hashes exist in the current execution branch.
- All source project files were XML-parsed. No compile or behavior claims are made without the required build/runtime verification.
