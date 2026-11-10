---
phase: 01-farm-planning-pilot
audited: 2026-11-10
status: OPEN_THREATS
asvs_level: 1
block_on: high
threats_closed: 21
threats_open_high: 3
threats_open_medium: 2
---

# Phase 1 Security Audit

Full review of threat registers across the eleven original plans and gap-closure plan 01-12. Threat IDs repeated as `T-01-SC` across plans are consolidated into one supply-chain finding. The pilot is intentionally in safe mode: climate is unavailable, validated agronomic/frame inputs are absent, and proposal confirmation is denied for incomplete drafts. No production secrets or external providers were used during verification.

## Closed Threats

| Threat ID | Severity | Evidence |
|---|---|---|
| T-01-01 | High | ASP.NET Identity password hashing and lockout, identity rate limiting, short-lived signed JWTs, hashed refresh tokens, rotation and revocation. |
| T-01-02 | High | Generic recovery responses, hashed/expiring recovery codes, bounded attempts, support role checks, and audit records. |
| T-01-03 | High | Refresh token reuse revokes token families; password reset/logout revoke sessions. |
| T-01-05, T-01-06 | High | Crop-cycle operations are farm-scoped; parcel links are checked against the same farm. |
| T-01-07 | Medium | Only currently usable, validated crop parameters are exposed; missing data remains missing. |
| T-01-08 | High | Durable outbox and atomic PostgreSQL worker claim; duplicate delivery is exercised by integration tests. |
| T-01-09 | High | Gemini credentials remain server-side; prompts omit farm/user identifiers and logbook content; provider payloads are not logged. |
| T-01-12, T-01-13 | High | Frame calculations use validated sourced rules; selection rechecks Owner/Administrator access. |
| T-01-14 | Medium | Planting-frame safe mode returns no options/capacity without validated rules and geometry. |
| T-01-15, T-01-16 | High | Corrections append linked entries, database triggers prevent mutation, attachment operations are farm-scoped, staging is finalized to immutable keys, and upload signatures expire quickly. |
| T-01-18, T-01-19 | High | Current membership is queried per request; cross-farm operations are denied and same-farm relationships are enforced. |
| T-01-20 | Medium | HTTP integration tests verify Development OpenAPI/Scalar and no documentation routes in Production. |
| T-01-21 | High | Worker validates every `IAiProposalProvider` result through `AiProposalDraftValidator` before persistence and stores provider provenance. |
| T-01-22 | Medium | Synthetic agronomic values are confined to test projects; runtime crop data remains unvalidated/missing. |
| T-01-23 | High | Unit tests cover typed draft validation and raw Gemini JSON parsing, including malformed/incomplete responses, numeric claims, unknown sources, and provenance. |
| T-01-26 | Critical | Integration tests assert cross-farm denial, immediate membership revocation, and 403 for an Administrator attempting Owner-only grant/revoke operations. |
| T-01-28 | High | Two consumers receive duplicate broker events; the atomic database claim prevents duplicate processing. |

## Open Blocking Threats

| Threat ID | Severity | Required closure |
|---|---|---|
| T-01-10 | High | Before enabling confirmable AI recommendations, bind confirmation to the exact proposal input and current versions of the crop parameters, climate forecast, and applicable rules. Current safe mode denies confirmation while required inputs are missing, so this is a release gate for turning on complete advice. |
| T-01-27 | High | Add PostgreSQL integration coverage for migrating legacy attachment rows and references through `ImmutableLogbookAttachmentStorage`; current tests cover immutability and staging-to-final behavior but not the legacy migration path. |
| T-01-SC | High | Record provenance and compatibility review for every pinned direct/transitive package used in production, including Scalar/OpenAPI, xUnit/Testcontainers, Npgsql, RabbitMQ, ASP.NET Identity/JWT, and storage SDK dependencies. This ID appears in multiple plan threat registers and is consolidated here. |

## Open Non-Blocking Threats

| Threat ID | Severity | Production gate |
|---|---|---|
| T-01-11 | Medium | Select a climate provider and enforce forecast freshness/expiry before enabling climate-backed recommendations. |
| T-01-17 | Medium | Verify the Supabase Storage bucket is private in the target project before enabling photo storage; application code authorizes reads and signs URLs but cannot prove external bucket privacy. |

## Verification Evidence

- `dotnet test src/AgroPlan.sln --no-restore -m:1` — 55 passed (46 unit + 9 PostgreSQL/RabbitMQ integration), 0 failed, 0 skipped.
- No production Gemini, climate, Supabase Storage, CloudAMQP, SMTP, or agronomic data source was contacted.
- Phase goal verification is `passed` (4/4 roadmap truths). Security remains `OPEN_THREATS` because the listed high-severity release gates are not yet closed.
