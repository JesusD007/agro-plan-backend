# Phase 3: Workforce and Offline Operations - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-10
**Phase:** 3 - Workforce and Offline Operations
**Areas discussed:** invitations and capacity, task evidence and verification, offline sync and conflicts, points and rewards, routes, calendar and partial failures

---

## Invitations and capacity

| Option | Description | Selected |
|--------|-------------|----------|
| Single-use email link | Worker joins via verified email; invite expires in seven days; administrator can renew/cancel; pending invitations count toward the five-worker basic tier cap. | ✓ |
| Other invitation or capacity policies | Alternative channel, duration, and cap behavior. | |

**User's choice:** Email single-use link, seven days, renew/cancel, pending invites count toward cap.

## Task evidence and verification

| Option | Description | Selected |
|--------|-------------|----------|
| Per-task photo rule; consent-based location/time; linked resubmission; wage confirmed on verification | Preserve earlier evidence revisions; wage amount/unit fixed at assignment; no AgroPlan payment execution. | ✓ |
| Uniform photo requirement or editable evidence | Apply one photo rule or replace past submission. | |

**User's choice:** Photo rule per task, location/time only with consent, linked new revision, retain earlier submission, admin-defined wage confirmed at verification.

## Offline sync and conflicts

| Option | Description | Selected |
|--------|-------------|----------|
| Per-operation outcomes, idempotent client IDs, preserve conflicts for explicit admin resolution | Independent operations continue; failed operations can retry; retain both task versions. | ✓ |
| Atomic batch or automatic overwrite | Fail/revert all operations or choose a version automatically. | |

**User's choice:** Per-operation results, continue independent operations, preserve stale conflicts for administrator resolution, stable client-generated operation ID.

## Points and rewards

| Option | Description | Selected |
|--------|-------------|----------|
| Objective task rules, rankings off by default, configured badges, manual non-payment rewards | Do not evaluate photo quality automatically. | ✓ |
| Automated quality scoring or always-on rankings | Automatic evidence assessment or default ranking. | |

**User's choice:** Objective extra-point criteria per task; rankings disabled unless enabled per farm; configured badges and manually granted rewards.

## Routes

| Option | Description | Selected |
|--------|-------------|----------|
| Neutral contract; unavailable until provider approval | Offline package has ordered stops and available geometry only. Stale routes require administrator review and reconfirmation. | ✓ |
| Select a managed/open provider now | Activate an actual provider during this phase. | |
| Maps/turn-by-turn or auto-refresh | Include full offline maps or silently update confirmed routes. | |

**User's choice:** Keep routes unavailable until provider approval; include ordered stops and available geometry offline, without base maps/turn-by-turn; mark changed routes stale for administrator review and reconfirmation.

## Calendar and partial failures

| Option | Description | Selected |
|--------|-------------|----------|
| New version with audit/notification; retry external calendar sync; isolate batch failures | Keep saved tasks when external sync fails and expose pending state. | ✓ |
| Overwrite or revert on failure | Replace history or roll back task because an external update failed. | |

**User's choice:** Version and audit schedule changes, notify worker, retry pending external calendar updates, isolate per-operation failures, and continue independent work.

## the agent's Discretion

- Entity/API boundaries, response codes, invitation and upload limits, retry mechanics, and route-provider approval criteria consistent with captured decisions.

## Deferred Ideas

None.
