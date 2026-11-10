---
phase: "1"
slug: "farm-planning-pilot"
status: passed
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-09"
---

# Phase 1 — Validation Strategy

> Validation strategy and observed results for Phase 1.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 on .NET 10. |
| **Config file** | `xunit.runner.json` per test project; package versions pinned in each project. |
| **Quick run command** | `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj -m:1`. No external services or Docker required. |
| **Full suite command** | `dotnet test src/AgroPlan.sln -m:1`; PostgreSQL and RabbitMQ integration fixtures use disposable Testcontainers and require Docker. |
| **API docs smoke command** | Covered by integration tests: Development `/openapi/v1.json` and `/scalar/` return 200 (`/scalar` redirects to `/scalar/`); Production maps neither route. |
| **Observed result** | Full solution: 55 passed, 0 failed, 0 skipped with .NET SDK 10.0.403 and Docker-backed PostgreSQL/RabbitMQ. |
| **Estimated runtime** | Unit suite is local and service-free; integration runtime depends on PostgreSQL and RabbitMQ container startup. |

---

## Sampling Rate

- Unit tests cover deterministic domain/application decisions and provider-dependent worker branches using fakes.
- Integration tests use PostgreSQL and RabbitMQ Testcontainers for persistence, authorization revocation, immutability, outbox, and worker delivery. Fixtures check Docker availability first and dynamically skip with a reason when no Docker-compatible engine is reachable.
- Worker integration publishes duplicate sowing events to two consumers and asserts the PostgreSQL claim/state version advances once through processing and completion.
- No test connects to Gemini, climate, SMTP, Supabase, or configured production endpoints.
- A successful build is a compile check, not behavioral verification; completion requires passing the relevant test commands.

---

## Per-Task Verification Map

| Requirement | Secure behavior to preserve | Planned evidence |
|-------------|-----------------------------|-----------------|
| REQ-pilot-accounts | Identity owns credentials; recovery uses a one-time code and support review; support never sees passwords. | Unit recovery cases plus API integration for sessions and support roles. |
| REQ-pilot-farms-parcels, REQ-rf-g-01 | Every farm-scoped operation checks current active membership; cross-farm resource links are rejected. | API/PostgreSQL integration tests including revocation between requests. |
| REQ-pilot-crop-cycle, REQ-rf-g-38 | Catalog contains five crops and stores parameter source/validation state; missing parameters remain missing. | Unit catalog assertions plus persistence/API integration. |
| REQ-pilot-sowing-plan, REQ-rf-g-10 | Provider-neutral AI and climate contracts produce a reviewable draft; incomplete/unsupported inputs suppress advice. | Worker integration with fake provider verifies safe mode, missing inputs, and zero provider calls; source provenance is asserted. |
| REQ-pilot-planting-frame, REQ-rf-g-11 | Validated deterministic rules and geometry calculate capacity; safe mode reports missing inputs without options or numbers. | Unit calculator cases and API integration for safe-mode response/confirmation gate. |
| REQ-pilot-manual-logbook, REQ-rf-g-05 | Corrections append linked records; optional photos are authorized for the same farm and finalized objects are immutable. | Unit correction tests plus PostgreSQL/API integration for triggers and upload finalization. |
| REQ-pilot-provider-neutral-ai | Application orchestration depends on a provider-neutral contract; Gemini remains an adapter. | Compile-time DI wiring and integration worker tests against a fake `IAiProposalProvider`. |
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
- [x] Unit and integration test commands pass on the current phase: 46 unit tests and 9 PostgreSQL/RabbitMQ integration tests, including raw Gemini parsing, provider-neutral AI output validation, owner/admin authorization, and producer registration with HTTP farm/parcel creation.
- [x] Existing runtime project scaffold is available.
- [x] No watch-mode flags.
- [ ] Feedback latency: not applicable.
- [x] Nyquist compliance: every Phase 1 requirement maps to automated evidence and the complete suite passed.

**Approval:** passed 2026-11-10; 55/55 tests passed. No production Gemini, climate, SMTP, or Supabase calls were made.


