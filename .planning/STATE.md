---
gsd_state_version: "1.0"
current_phase: 01
current_phase_name: Farm Planning Pilot
status: executing
stopped_at: Completed 01-05-PLAN.md
last_updated: "2026-11-10T04:47:13.573Z"
last_activity: 2026-11-10
state_head: 4bab07bebe34708ad54ebb083d112e2cab8aa23d
progress:
  total_phases: 6
  completed_phases: 0
  total_plans: 8
  completed_plans: 5
  percent: 0
last_activity_desc: Initialized planning from the approved document set.
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-11-09)

**Core value:** Producers can rely on secure, farm-scoped operational records and reviewable assistance from planting through harvest.
**Current focus:** Phase 1: Farm Planning Pilot

## Current Position

Phase: 01 (Farm Planning Pilot) — READY TO EXECUTE
Plan: 6 of 8 in current phase
Status: Ready to execute
Last activity: 2026-11-10

Progress: ░░░░░░░░░░ [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:** No plans completed yet.

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 01 P01 | 13m | 2 tasks | 13 files |
| Phase 01 P02 | 18m | 2 tasks | 17 files |
| Phase 01 P03 | 28m | 2 tasks | 16 files |
| Phase 01 P04 | 9m | 2 tasks | 13 files |
| Phase 01 P05 | 75m | 2 tasks | 18 files |

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

### Pending Todos

None yet.

### Blockers/Concerns

- Climate source and crop-specific numeric agronomic sources remain explicitly unselected; neither may be activated for production until selected and validated.
- Plan 01-01 is complete on `develop`; the .NET 10 compile gate passed. Continue with Plan 01-02.

## Deferred Items

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Scope | Worker, expanded farm operations, AI extensions, Mercado, transport, and client experience beyond the pilot | Planned for later roadmap phases | 2026-11-09 | v1 roadmap |

## Session Continuity

Last session: 2026-11-10T04:47:13.528Z
Stopped at: Completed 01-05-PLAN.md
Resume file: .planning/phases/01-farm-planning-pilot/01-06-PLAN.md
