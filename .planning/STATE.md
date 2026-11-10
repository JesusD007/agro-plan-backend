---
gsd_state_version: "1.0"
current_phase: 3
current_phase_name: Workforce and Offline Operations
status: executing
stopped_at: Completed 03-01-PLAN.md
last_updated: "2026-11-10T20:49:00.544Z"
last_activity: 2026-11-10
last_activity_desc: Phase 3 execution started
state_head: 48b464fa54b3d92a46db2cc20bb41c93bdc0f1b4
progress:
  total_phases: 6
  completed_phases: 2
  total_plans: 27
  completed_plans: 21
  percent: 33
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-11-09)

**Core value:** Producers can rely on secure, farm-scoped operational records and reviewable assistance from planting through harvest.
**Current focus:** Phase 3 — Workforce and Offline Operations

## Current Position

Phase: 3 (Workforce and Offline Operations) — EXECUTING
Plan: 2 of 7
Status: Ready to execute
Last activity: 2026-11-10 — Phase 3 execution started

Progress: ████████░░ [███░░░░░░░] 33%

## Performance Metrics

**Velocity:** No plans completed yet.

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1 | 12 | - | - |
| 2 | 8 | - | - |
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 01 P01 | 13m | 2 tasks | 13 files |
| Phase 01 P02 | 18m | 2 tasks | 17 files |
| Phase 01 P03 | 28m | 2 tasks | 16 files |
| Phase 01 P04 | 9m | 2 tasks | 13 files |
| Phase 01 P05 | 75m | 2 tasks | 18 files |
| Phase 01 P06 | 18m | 2 tasks | 14 files |
| Phase 01 P07 | 12m | 2 tasks | 17 files |
| Phase 01 P08 | 50min | 2 tasks | 17 files |
| Phase 2 P1 | 9m | 2 tasks | 9 files |
| Phase 2 P02 | 8min | 2 tasks | 13 files |
| Phase 02 P03 | 20min | 2 tasks | 9 files |
| Phase 2 P04 | 23m | 2 tasks | 10 files |
| Phase 02 P05 | 35m | 2 tasks | 8 files |
| Phase 02 P06 | 16m | 2 tasks | 7 files |
| Phase 2 P7 | 20min | 2 tasks | 12 files |
| Phase 02 P08 | 22m | 2 tasks | 16 files |
| Phase 03 P01 | 25min | 2 tasks | 11 files |

## Accumulated Context

### Decisions

- Use the user-confirmed ASP.NET Core/.NET, Supabase, CloudAMQP, and SignalR architecture proposal as the implementation baseline.
- Deliver the six confirmed pilot capabilities before expanded Gestão, Mercado, and transport scope.
- Consult the excluded actor/function flows source during later phase planning.
- [Phase 01]: Issue first-party short-lived signed JWT access tokens with server-side signing material.
- [Phase 01]: Validate active owner/administrator farm membership in PostgreSQL for every farm-scoped request.
- [Phase 01]: Create the farm and owner membership atomically in one PostgreSQL transaction.
- [Phase 01]: Refresh tokens are opaque, hashed, rotated, and replay revokes the token family.
- [Phase 01]: Account recovery requires email verification followed by audited Support-role review; reset revokes all refresh sessions.
- [Phase 01]: Only farm owners can grant or revoke farm-administrator membership; each change creates an audit record.
- [Phase 01]: Parcel queries and relationships are scoped by farm id; membership authorization is re-read from PostgreSQL per request.
- [Phase 01]: Pilot crop catalog seeds only user-approved crop identities and exposes missing agronomic inputs; no numeric values are invented.
- [Phase 01]: Persist sowing requests and outbox events atomically; confirmed, idempotent broker delivery and advisory per-user SignalR notifications leave PostgreSQL authoritative.
- [Phase 01]: Use official Google.GenAI 1.28.0 with server-side credentials; climate remains unavailable until a source is selected and validated, and incomplete drafts cannot be confirmed.
- [Phase 01]: Calculate planting-frame capacity only from currently valid, sourced spacing rules and validated usable parcel geometry; missing inputs suppress estimates, and owner/administrator confirmation persists the selected frame.
- [Phase 01]: Use AWS SDK S3 SigV4 for Supabase direct uploads to meet the ten-minute expiry; keep keys server-side and require a private, size-limited bucket.
- [Phase 2]: Application-frequency assessments use only one exact, current, validated product/crop/use/jurisdiction rule; otherwise the API returns unavailable.
- [Phase 2]: Client-supplied PHI cannot authorize harvest; exact label evidence is evaluated by the harvest-safety workflow.
- [Phase 2]: Only exact validated pesticide product/crop/use PHI evidence authorizes harvest.
- [Phase 2]: PHI remains distinct from MRL and worker re-entry intervals.
- [Phase 2]: Validated label evidence and harvest records are immutable; changes append linked versions.
- [Phase 02]: Calendar estimates require a confirmed sowing date and a current validated crop template.
- [Phase 02]: Estimated harvest windows are informational and do not bypass exact validated PHI eligibility.
- [Phase 2]: Advisor grants are separate, read-only, limited to explicit report scopes and a maximum 90-day interval, and checked against PostgreSQL for every request.
- [Phase 03]: Worker invitations use SHA-256 token digests, seven-day expiry, and verified normalized-email matching.
- [Phase 03]: Basic worker capacity counts active Workers plus pending invitations; Premium entitlement is trusted server-provisioned state.
- [Phase 03]: Invitation issue and renewal require a configured HTTPS client acceptance URL.

### Pending Todos

None yet.

### Blockers/Concerns

- Climate source and crop-specific numeric agronomic sources remain explicitly unselected; neither may be activated for production until selected and validated.
- Climate, usable parcel geometry, and crop-specific frame rules remain unavailable until a source and validation process are approved; frame capacity therefore remains withheld.

## Deferred Items

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Scope | Worker, expanded farm operations, AI extensions, Mercado, transport, and client experience beyond the pilot | Planned for later roadmap phases | 2026-11-09 | v1 roadmap |

## Session Continuity

Last session: 2026-11-10T20:48:59.418Z
Stopped at: Completed 03-01-PLAN.md
Resume file: 03-02-PLAN.md
