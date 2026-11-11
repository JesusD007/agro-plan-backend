---
phase: "04"
slug: "applied-farm-intelligence"
status: verified
threats_open: 0
asvs_level: 1
created: "2026-11-11"
---

# Phase 04 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

## Trust Boundaries

| Boundary | Description | Data Crossing |
|---|---|---|
| Client to API | Authenticated farm-scoped submissions, reads, confirmations and reviews | Identity, farm records, consent, private media metadata |
| API to PostgreSQL/outbox | Validated state transitions and durable job requests | Farm IDs, internal source IDs, authorization and audit state |
| Worker to providers | Explicitly approved, consented media or bounded source projections | Audio/photos, constrained summaries; no farm/user/database identifiers in prompts |
| API/Worker to private storage and RabbitMQ | Short-lived private object access and durable operation events | Private media bytes/object keys and bounded event references |
| Advisor/agronomist access | Read-only grant scopes or explicit verified farm authorization | Scope-filtered summaries/source rows or authorized review data |

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|---|---|---|---|---|---|---|
| T-04-01 | S | Voice authorization and consent | High | Mitigate | Recheck active membership, assignment and per-audio consent before media/provider access and result publication. | closed |
| T-04-02 | T | Transcription output and confirmation | High | Mitigate | Strict typed/schema/semantic validation; only explicit confirmation appends one immutable logbook entry. | closed |
| T-04-03 | I | Voice media access | High | Mitigate | Private farm-scoped media and draft queries; no public media URLs. | closed |
| T-04-04 | D | Audio upload and retries | Medium | Mitigate | Enforce format, duration and byte bounds; cap processing attempts and expire/delete audio. | closed |
| T-04-SC (04-01) | T | Package installation | High | Mitigate | No phase-added NuGet packages. | closed |
| T-04-05 | S | Summary requests, sharing and source reads | High | Mitigate | Recheck current membership and advisor grant/scope for every share, summary read and source read. | closed |
| T-04-06 | T | Deterministic totals and narrative output | High | Mitigate | Keep DB IDs server-side behind random per-job aliases; validate model references and output against deterministic projection. | closed |
| T-04-07 | I | Advisor summary scope | High | Mitigate | Filter source rows/projection by current grant; omit narrative from advisor responses. | closed |
| T-04-08 | D | Weekly summary projection and retries | Medium | Mitigate | Bound attempts; recover abandoned leases; exhaust to Failed with deterministic projection retained and no endless requeue. | closed |
| T-04-SC (04-02) | T | Package installation | High | Mitigate | No phase-added NuGet packages. | closed |
| T-04-09 | E | Agronomist review endpoint | High | Mitigate | Require trusted current verification and explicit non-revoked farm authorization on each review/assignment operation. | closed |
| T-04-10 | I | Symptom photos and report reads | High | Mitigate | Private farm-scoped media; short-lived access and current report/farm authorization on reads. | closed |
| T-04-11 | T | Observation and human review state | High | Mitigate | AI output is limited to visible signs/evidence requests; diagnosis/treatment are only available in authorized human review. | closed |
| T-04-12 | R | Review decisions and media deletion | Medium | Mitigate | Append review decisions and deletion state/retries while retaining report/history. | closed |
| T-04-13 | D | Photo intake and observation retries | Medium | Mitigate | Enforce image bounds and cap attempts; exhausted jobs ACK safely and leave the report available for manual review. | closed |
| T-04-SC (04-03) | T | Package installation | High | Mitigate | No phase-added NuGet packages. | closed |
| T-04-14 | E | Production provider activation | High | Mitigate | Default off; require enabled operation, named provider, exact verified operation and approved evidence; speech also requires bilingual evidence gates. | closed |
| T-04-15 | T | Consumer-to-provider routing | High | Mitigate | Register each consumer only with its matching provider operation; API and Worker share the same activation predicate. | closed |
| T-04-16 | I | Logs and API responses | High | Mitigate | Redact payloads and identifiers; logs record exception type only and tests check for sensitive markers. | closed |
| T-04-17 | D | Worker startup and provider failures | Medium | Mitigate | Fail closed on missing provider/configuration; bound retries and preserve retry/failure state. | closed |
| T-04-SC (04-04) | T | Package installation | High | Mitigate | No phase-added NuGet packages. | closed |

*Status: open · closed · open — below high threshold (non-blocking).*  
*Only open threats at or above the configured high threshold count toward `threats_open`.*

## Accepted Risks Log

No accepted risks.

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|---|---:|---:|---:|---|
| 2026-11-11 | 21 | 21 | 0 | GSD security auditor |

## Sign-Off

- [x] All threats have a disposition.
- [x] Accepted risks documented (none accepted).
- [x] `threats_open: 0` confirmed.
- [x] `status: verified` set in frontmatter.

**Approval:** verified 2026-11-11
