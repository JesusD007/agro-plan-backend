# Phase 1 External API and Capability Coverage

Prepared during planning for `01-farm-planning-pilot`. Every external API/service capability in scope has an explicit decision and owning plan. No account, secret, hosted resource, or deployment is provisioned by this artifact.

## Integration Coverage

| capability | decision | reason |
|---|---|---|
| Supabase PostgreSQL | INTEGRATE | Authoritative Identity, domain, job, outbox and audit persistence with tenant-aware relations and transactions. Plans 01-01 through 01-08. |
| Supabase Storage | INTEGRATE | Private optional logbook photos; API checks live membership, issues short-lived upload authorization, and stores metadata only. Plan 01-08. |
| CloudAMQP RabbitMQ | INTEGRATE | Durable jobs via PostgreSQL outbox, publisher confirms and idempotent Worker. Plan 01-08. |
| Gemini API via Google AI Studio | INTEGRATE | Google.GenAI Worker adapter, server key, configurable model, schema-checked drafts. Verify package and .NET 10 compatibility before pinning. Plan 01-06. |
| Climate data API | OPT-OUT | D-05 leaves the source unselected. Pilot reports climate as unavailable; validate a source before enabling it in production. Plan 01-06. |
| Email delivery for recovery | INTEGRATE | Implement IEmailSender adapter and expiring one-time recovery flow; choose and configure a delivery service before production activation. Plan 01-02. |
| SignalR | INTEGRATE | Transient job status notifications only; API/PostgreSQL hold authoritative state and hub access rechecks current membership. Plan 01-08. |
| Render deployment | OPT-OUT | No account/service provisioning or deployment is included; Docker entry points remain deployable by the separately authorized operator. Plan 01-01. |
| Third-party OIDC identity provider | OPT-OUT | Pilot uses first-party ASP.NET Core Identity credentials and signed API JWTs for first-party clients; no third-party identity federation requirement exists. Plans 01-01 and 01-02. |
| Client UI, voice, offline sync | OPT-OUT | This is a backend API phase; visual interfaces, voice and offline sync are assigned to other repos or later phases. |
| Worker/advisor roles | OPT-OUT | Pilot activates owner and farm administrator only; worker and advisor workflows belong to later roadmap phases. |
| Marketplace, buyer, transport, rental and pricing integrations | OPT-OUT | These capabilities are assigned to later roadmap phases. |
| Automated test frameworks/providers | OPT-OUT | User-authorized scope prohibits adding or running tests; compile-only `dotnet build src/AgroPlan.sln` is the planned automated check after .NET SDK 10 is available. |

## Source Coverage Audit

| Source | ID | Feature or decision | Plan | Status | Notes |
|---|---|---|---|---|---|
| GOAL | — | Producer manages core farm data, reviews planting assistance, and keeps manual logbook | 01-01–01-08 | COVERED | Backend APIs, no client UI. |
| REQ | REQ-pilot-accounts | Account creation, management, mediated recovery | 01-01, 01-02 | COVERED | Identity and support-reviewed email recovery. |
| REQ | REQ-pilot-farms-parcels | Farm and parcel management | 01-01, 01-03 | COVERED | Request-time membership and tenant-safe relations. |
| REQ | REQ-pilot-crop-cycle | Crop selection and cycle | 01-04 | COVERED | Five-crop catalog and parcel-linked cycle. |
| REQ | REQ-pilot-sowing-plan | AI sowing proposal and climate review | 01-05, 01-06 | COVERED | Async draft; climate unavailable until source selection. |
| REQ | REQ-pilot-planting-frame | AI-assisted planting frame | 01-07 | COVERED | Deterministic validated capacity and confirmation. |
| REQ | REQ-pilot-manual-logbook | Manual logbook | 01-08 | COVERED | Immutable linked corrections and photos. |
| REQ | REQ-rf-g-01 | Farm/parcel name, location, area, crop | 01-01, 01-03, 01-04 | COVERED | Farm, parcel and crop-cycle attributes. |
| REQ | REQ-rf-g-05 | Daily activity log and optional photos | 01-08 | COVERED | Activity types and private storage. |
| REQ | REQ-rf-g-10 | Sowing factors, missing data, scenarios, uncertainty, validity | 01-05, 01-06 | COVERED | Preserve source, freshness, inputs and model metadata. |
| REQ | REQ-rf-g-11 | Capacity from usable area/aisles/validated distances; producer selects | 01-07 | COVERED | Deterministic math and explicit confirmation. |
| REQ | REQ-rf-g-38 | Crop catalog with framework, stages, cycle length, typical dates | 01-04, 01-07 | COVERED | Seed only validated values; expose missing fields. |
| RESEARCH | — | .NET 10 LTS, compatible EF Core/Npgsql and package verification | 01-01–01-08 | COVERED | SDK 10 prerequisite; verify compatibility/provenance. |
| RESEARCH | — | Identity token distinction from JWT/OIDC | 01-01, 01-02 | COVERED | First-party signed JWT and opaque rotating refresh. |
| RESEARCH | — | Outbox, confirms, idempotent consumers, PostgreSQL truth | 01-05 | COVERED | Durable async lifecycle and idempotence. |
| RESEARCH | — | AI schema/provenance validation and human review | 01-06, 01-07 | COVERED | No automatic promotion to confirmed data. |
| RESEARCH | — | No validated numeric agronomic inputs found | 01-04, 01-06, 01-07 | COVERED | Missing markers; numeric advice gated on evidence. |
| RESEARCH | — | Private direct Storage upload and finalization verification | 01-08 | COVERED | Short-lived authorization and metadata. |
| CONTEXT | D-01 | Check active farm membership in DB every scoped request | 01-01–01-08 | COVERED | Includes confirmation, uploads and hub access. |
| CONTEXT | D-02 | Support-mediated recovery with no password exposure | 01-02 | COVERED | Expiry, attempt/rate limits, review and revocation. |
| CONTEXT | D-03 | Pilot owner and farm admin only | 01-01–01-08 | COVERED | Worker/advisor excluded. |
| CONTEXT | D-04 | Official Google.GenAI from Worker, server key, configurable model | 01-06, 01-07 | COVERED | Verify SDK before pinning. |
| CONTEXT | D-05 | Climate source unselected; contract now, source before production | 01-06 | COVERED | Explicit unavailable state until validated source. |
| CONTEXT | D-06 | AI outputs drafts; approval; deterministic agronomy | 01-06, 01-07 | COVERED | Review and confirmation boundary. |
| CONTEXT | D-07 | Five pilot crops | 01-04, 01-06, 01-07 | COVERED | plátano, yuca, batata, arroz, habichuela. |
| CONTEXT | D-08 | Missing validated values visible, never invented | 01-04, 01-06, 01-07 | COVERED | Withhold unsupported values. |
| CONTEXT | D-09 | Preserve parameter source/validation/region/validity | 01-04, 01-06, 01-07 | COVERED | Catalog and proposal snapshots. |
| CONTEXT | D-10 | Immutable logbook and linked corrections | 01-08 | COVERED | Original retained and linked. |
| CONTEXT | D-11 | API-authorized optional Supabase photo storage | 01-08 | COVERED | Short-lived authorization and DB metadata. |
| CONTEXT | D-12 | PostgreSQL truth, API/Worker, Rabbit, idempotence, SignalR | 01-05 | COVERED | PostgreSQL authoritative; SignalR advisory. |

## Security and Validation Notes

- Security enforcement: OWASP ASVS Level 1. Each plan contains STRIDE threats and high-severity mitigations; package-install tampering gate is present in every plan.
- No application source code, test files, test commands, external resources, actual secrets, or deployments are created by planning.
- `dotnet build src/AgroPlan.sln` is an implementation-time compile check after the solution exists; it is not evidence of runtime behavior. Manual acceptance scenarios in plans have not been run.
