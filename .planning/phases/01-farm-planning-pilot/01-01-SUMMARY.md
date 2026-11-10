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
  duration: 22m
  completed_date: 2026-11-10
  commits: 4
  plan_head_before: 9a641afd842a0dc568e6c9ffe9af54893992baf6
  plan_head_after: e9bb103c6603811b8ffad756bc8adeb1f473adea
actuals:
  tokens: 10832
  tasks: 2
  commits: 4
status: complete
---

# Phase 1 Plan 1: Identity and Farm Boundary Summary

Implemented the .NET solution skeleton and the first persisted account-to-farm path. Registration and sign-in issue short-lived signed JWTs; farm reads check the current database membership and constrain the query to the same active farm role.

## Progress

| Plan task | Status | Commit |
|---|---|---|
| Task 1: .NET hosts, Identity, farm persistence and request-time authorization | Complete; solution build passed | `a859ae9`, `e9bb103` |
| Task 2: Separate API and Worker container packaging | Complete; Dockerfiles reviewed statically | `37a8b22` |

The compile-only command `dotnet build src/AgroPlan.sln` passed with .NET SDK 10.0.403 and 0 warnings/errors. No tests were added or run, and no manual acceptance scenarios were executed.

## Security and Persistence Notes

- Account registration uses ASP.NET Core Identity password hashing, unique email validation, password policy, lockout and IP-partitioned rate limiting.
- JWT signing uses a server-side setting of at least 32 bytes; Development falls back to an ephemeral process key, while other environments fail startup without the configured secret.
- Farm creation commits the farm and owner membership together. Reads require current active owner/administrator membership and repeat that condition in the PostgreSQL query.
- The initial SQL migration creates the Identity and farm tables in separate `identity` and `agro` schemas. It has not been applied to a database.
- The API image uses the .NET 10 ASP.NET runtime; the Worker image uses the .NET 10 runtime. Both run as the image's non-root application user and contain no committed credentials. No image build or deployment was performed.
- The migration was not applied to a database.

## Known Stubs

| File | Reason |
|---|---|
| `src/AgroPlan.Worker/Program.cs` | The Worker host remains idle until the later async processing plans add consumers. |

## Deferred Verification

- Registration, sign-in, farm creation, cross-farm denial and membership revocation scenarios remain unexecuted acceptance criteria.

## Self-Check: PASSED

- The solution build passed with 0 warnings and 0 errors.
- The API and Worker Dockerfiles have separate .NET 10 build and runtime entry points; no secrets or deployment steps were added.
- Task 1 and Task 2 commit hashes exist in the execution branch. No tests or manual behavior checks were run.
