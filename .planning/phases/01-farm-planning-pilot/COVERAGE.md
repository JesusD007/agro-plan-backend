# Phase 1 External API and Capability Coverage

Prepared during planning for `01-farm-planning-pilot`. Every external API/service capability in scope has an explicit decision and owning plan. No account, secret, hosted resource, or deployment is provisioned by this artifact.

## Integration Coverage

| Capability / external boundary | Decision | Plan | Coverage rationale and required behavior |
|---|---|---|---|
| Supabase PostgreSQL | INTEGRATE | 01-01 through 01-05 | Authoritative Identity/domain/job/outbox/audit persistence; tenant-aware relationships and transactions. |
| Supabase Storage | INTEGRATE | 01-05 | Private optional logbook photos; API verifies current membership, issues expiring upload authorization, and stores metadata/reference in PostgreSQL. No binary in RabbitMQ. |
| CloudAMQP RabbitMQ | INTEGRATE | 01-03, 01-04 | Durable asynchronous proposal jobs via PostgreSQL outbox, publisher confirms, stable identifiers and idempotent Worker processing. |
| Gemini API / Google AI Studio | INTEGRATE | 01-03, 01-04 | Official `Google.GenAI` C# SDK in Worker; server-side key, configurable model, structured output validation, bounded retries/timeouts, draft-only lifecycle. Verify official docs, NuGet provenance and .NET 10 compatibility before pinning. |
| Climate data API | OPT OUT of live provider; INTEGRATE provider contract | 01-03 | Provider source is unselected. Implement contract and explicit unavailable response with no fabricated climate values. Choose and validate a source before production activation. |
| Email delivery for account recovery | INTEGRATE contract and configurable adapter; provider selection required before production | 01-01 | Send expiring one-time recovery code to account email and support review status. Never log the code/password or allow support to see/set password. Configure delivery through server-side settings; no live provider chosen or provisioned here. |
| SignalR | INTEGRATE | 01-03 | Transient job status notifications only; clients retrieve authoritative state from API/PostgreSQL. Recheck farm membership for hub group access/operations. |
| Render services | CONFIGURE deployment artifacts only; no deployment | 01-01 | Separate API and Worker Docker entry points suitable for separate Render services. No account setup, service creation, or deployment in planning scope. |
| Identity API bearer token format | OPT OUT | 01-01 | Built-in API token format is proprietary and not JWT/OIDC. Use Identity for credential lifecycle with first-party short-lived signed JWT access tokens and hashed, rotating opaque refresh tokens. |
| Client UI, mobile UI, voice, offline sync | OPT OUT | — | Backend API contracts are in pilot scope; client visual/interface work and offline/voice features belong to other repos or later roadmap phases. |
| Worker/advisor membership and permissions | OPT OUT | — | Pilot activates owner/farm administrator only. Worker and advisor flows are mapped to later phases. |
| Marketplace, buyers, transport, rentals, pricing integrations | OPT OUT | — | These capabilities are assigned to later roadmap phases and are outside the six-capability pilot. |
| Automated test providers/frameworks | OPT OUT | — | User-authorized planning scope prohibits adding or running test files/commands. Future compile-only command is `dotnet build src/AgroPlan.sln`; manual acceptance scenarios remain unexecuted. |

## Source Coverage Audit

| Source | ID | Feature or decision | Plan | Status | Notes |
|---|---|---|---|---|---|
| GOAL | — | Producer manages core farm data, reviews planting assistance, and keeps manual logbook | 01-01–01-05 | COVERED | End-to-end backend API capabilities, no client UI. |
| REQ | REQ-pilot-accounts | Account creation, management, mediated recovery | 01-01 | COVERED | Identity + token/session lifecycle + support-reviewed email code. |
| REQ | REQ-pilot-farms-parcels | Farm and parcel management | 01-01 | COVERED | Request-time membership and tenant-safe relations. |
| REQ | REQ-pilot-crop-cycle | Crop selection and cycle | 01-02 | COVERED | Five-crop catalog and parcel-linked cycle. |
| REQ | REQ-pilot-sowing-plan | AI sowing proposal and climate review | 01-03 | COVERED | Async draft, climate contract, explicit missing/unavailable state and approval. |
| REQ | REQ-pilot-planting-frame | AI-assisted planting frame | 01-04 | COVERED | Deterministic validated capacity plus explanatory draft and confirmation. |
| REQ | REQ-pilot-manual-logbook | Manual logbook | 01-05 | COVERED | Immutable linked corrections, optional photos. |
| REQ | REQ-rf-g-01 | Farm/parcel name, location, area, crop | 01-01, 01-02 | COVERED | Farm/parcel and crop-cycle attributes. |
| REQ | REQ-rf-g-05 | Daily activity log and optional photos | 01-05 | COVERED | Activity types and private storage flow. |
| REQ | REQ-rf-g-10 | Sowing factors, missing data, scenarios, uncertainty, validity | 01-03 | COVERED | Proposal retains source/freshness/input/model metadata; no fabricated weather or parameters. |
| REQ | REQ-rf-g-11 | Capacity from usable area/aisles/validated distances; producer selects | 01-04 | COVERED | Deterministic math and explicit confirmation; missing-input path. |
| REQ | REQ-rf-g-38 | Crop catalog with framework, stages, cycle length, typical dates | 01-02, 01-04 | COVERED | Include only source-validated values; expose missing fields for unsupported crop data. |
| RESEARCH | — | .NET 10 LTS; compatible EF Core/Npgsql; no NuGet pin before verification | 01-01–01-05 | COVERED | Build targets net10.0; package verification required at pin time. |
| RESEARCH | — | Identity API tokens are proprietary, not JWT/OIDC | 01-01 | COVERED | Explicit first-party JWT/opaque refresh selection. |
| RESEARCH | — | Outbox, broker confirms, idempotent consumers, PostgreSQL truth | 01-03, 01-04 | COVERED | Durable job lifecycle with failure states. |
| RESEARCH | — | AI output schema/provenance validation and human review | 01-03, 01-04 | COVERED | No automatic promotion to confirmed data. |
| RESEARCH | — | No validated numeric agronomic inputs found | 01-02–01-04 | COVERED | Named missing-input markers across all five crops. |
| RESEARCH | — | Private direct Storage upload and finalization verification | 01-05 | COVERED | Expiring API authorization and relational metadata only. |
| CONTEXT | D-01 | Check active farm membership in DB every scoped request | 01-01–01-05 | COVERED | Including proposal confirmation and upload/hub access. |
| CONTEXT | D-02 | Support-mediated recovery, code/email, no password visible to support | 01-01 | COVERED | Expiry/attempt/replay/session revocation controls. |
| CONTEXT | D-03 | Pilot owner and farm admin only | 01-01–01-05 | COVERED | Worker/advisor excluded per roadmap. |
| CONTEXT | D-04 | Official Google.GenAI from Worker, server-side key, configurable model | 01-03, 01-04 | COVERED | SDK verification before package pin. |
| CONTEXT | D-05 | Climate provider unselected; contract now, source before production | 01-03 | COVERED | Explicit unavailable result until source is selected. |
| CONTEXT | D-06 | AI outputs drafts; explicit owner/admin approval; deterministic agronomy | 01-03, 01-04 | COVERED | Persisted review/confirmation boundary. |
| CONTEXT | D-07 | Five pilot crops | 01-02–01-04 | COVERED | plátano, yuca, batata, arroz, habichuela. |
| CONTEXT | D-08 | Missing validated values are visible, never invented | 01-02–01-04 | COVERED | Withhold unsupported numeric outputs. |
| CONTEXT | D-09 | Preserve parameter source/validation/region/validity metadata | 01-02–01-04 | COVERED | Stored with catalog and proposal snapshots. |
| CONTEXT | D-10 | Immutable logbook and linked corrective operations | 01-05 | COVERED | Original row retained and linked. |
| CONTEXT | D-11 | API-authorized optional Supabase photo storage | 01-05 | COVERED | Short-lived upload authorization and DB metadata. |
| CONTEXT | D-12 | PostgreSQL truth, API/Worker, Rabbit outbox, idempotence, SignalR status | 01-03–01-04 | COVERED | Worker completion and status are durable; SignalR is advisory. |

## Security and Validation Notes

- Security enforcement: OWASP ASVS Level 1. Each plan contains STRIDE threats and high-severity mitigations; package-install tampering gate is present in every plan.
- No application source code, test files, test commands, external resources, actual secrets, or deployments are created by planning.
- `dotnet build src/AgroPlan.sln` is an implementation-time compile check after the solution exists; it is not evidence of runtime behavior. Manual acceptance scenarios in plans have not been run.
