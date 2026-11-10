---
phase: "1"
slug: "farm-planning-pilot"
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-09"
---

# Phase 1 — Validation Strategy

> Validation scope for Phase 1. Automated coverage is being added by plans 01-10 and 01-11.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 on .NET 10. |
| **Config file** | `xunit.runner.json` per test project; package versions pinned in each project. |
| **Quick run command** | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj`. No external services or Docker required. |
| **Full suite command** | `dotnet test src/AgroPlan.sln`; PostgreSQL and RabbitMQ integration fixtures use disposable Testcontainers and require Docker. |
| **API docs smoke command** | Covered by integration tests: Development `/openapi/v1.json` and `/scalar` respond; Production does not map either route. |
| **Estimated runtime** | Unit suite is local and service-free; integration runtime depends on PostgreSQL and RabbitMQ container startup. |

---

## Sampling Rate

- Unit tests cover deterministic domain/application decisions and provider-dependent worker branches using fakes.
- Integration tests use PostgreSQL and RabbitMQ Testcontainers for persistence, authorization revocation, immutability, outbox, and worker delivery. Fixtures check Docker availability first and dynamically skip with a reason when no Docker-compatible engine is reachable.
- The current worker integration case exercises single delivery and durable safe-mode transitions; duplicate/concurrent RabbitMQ delivery and atomic worker-claim assertions remain unverified and must be added before production reliance.
- No test connects to Gemini, climate, SMTP, Supabase, or configured production endpoints.
- A successful build is a compile check, not behavioral verification; completion requires passing the relevant test commands.

---

## Per-Task Verification Map

| Requirement | Secure behavior to preserve | Planned evidence |
|-------------|-----------------------------|-----------------|
| REQ-pilot-accounts | Identity owns credentials; recovery uses a one-time code and support review; support never sees passwords. | Unit recovery cases plus API integration for sessions and support roles. |
| REQ-pilot-farms-parcels, REQ-rf-g-01 | Every farm-scoped operation checks current active membership; cross-farm resource links are rejected. | API/PostgreSQL integration tests including revocation between requests. |
| REQ-pilot-crop-cycle, REQ-rf-g-38 | Catalog contains five crops and stores parameter source/validation state; missing parameters remain missing. | Unit catalog assertions plus persistence/API integration. |
| REQ-pilot-sowing-plan, REQ-rf-g-10 | Provider-neutral AI and climate contracts produce a reviewable draft; incomplete/unsupported inputs suppress advice. | Worker unit tests with fake providers plus async integration and provenance checks. |
| REQ-pilot-planting-frame, REQ-rf-g-11 | Validated deterministic rules and geometry calculate capacity; safe mode reports missing inputs without options or numbers. | Unit calculator cases and API integration for safe-mode response/confirmation gate. |
| REQ-pilot-manual-logbook, REQ-rf-g-05 | Corrections append linked records; optional photos are authorized for the same farm and finalized objects are immutable. | Unit correction tests plus PostgreSQL/API integration for triggers and upload finalization. |
| REQ-pilot-provider-neutral-ai | Application orchestration depends on a provider-neutral contract; Gemini remains an adapter. | Compile-time DI wiring and worker tests against a fake `IAiProposalProvider`. |
| REQ-pilot-api-docs | OpenAPI is inspectable through Scalar in development and absent from production routes. | HTTP integration smoke tests for document and UI mapping. |
| REQ-pilot-test-suite | Every pilot capability and critical tenant/data integrity boundary has repeatable automated coverage. | Unit suite and Docker-backed integration suite. |

---

## Wave 0 Requirements

- [x] Establish the solution and runtime projects.
- [x] Select xUnit v3 for the authorized .NET 10 test projects.
- [x] Add isolated unit and integration test projects; keep production provider credentials out of fixtures.

---

## Manual-Only Verifications

External production services remain unprovisioned. Integration tests must use disposable containers and fakes; production climate and agronomic sources remain intentionally unselected, and their absence is an asserted safe-mode result.

---

## Validation Sign-Off

- [x] All tasks have an authorized verification approach.
- [ ] Unit and integration test commands pass on the current phase. Integration tests build with .NET SDK 10.0.403, but this runner did not produce test results: the Docker engine pipe is inaccessible and even the OpenAPI-only filtered test stalled before reporting. Run `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj -m:1` and `dotnet test src/AgroPlan.sln -m:1` on a Docker-enabled runner before sign-off.
- [x] Existing runtime project scaffold is available.
- [x] No watch-mode flags.
- [ ] Feedback latency: not applicable.
- [ ] Set `nyquist_compliant: true` only after the complete suite passes and all Phase 1 requirements map to evidence.

**Approval:** pending


