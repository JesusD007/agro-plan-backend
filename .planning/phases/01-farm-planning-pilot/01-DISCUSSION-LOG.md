# Phase 1: Farm Planning Pilot - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-09
**Phase:** 1-Farm Planning Pilot
**Areas discussed:** authentication and farm authorization, AI and climate, crop catalog and agronomic rules, manual logbook and account recovery

---

## Authentication and farm authorization

| Option | Description | Selected |
|--------|-------------|----------|
| Validate membership on each request | Check current active farm membership in the database so revocation takes effect immediately. | ✓ |
| Trust role claims until token expiry | Farm permissions in the token remain valid until it expires. | |
| Other option | User may provide another authorization approach. | |

**User's choice:** Validate membership on each request.
**Notes:** The pilot uses owner and farm-administrator workflows; workers and advisors remain later-phase capabilities.

---

## AI and climate integration

| Option | Description | Selected |
|--------|-------------|----------|
| Contracts now, select climate source before production | Define provider interfaces and make the final climate selection before production. | ✓ |
| Select a vendor now | Choose both data sources immediately. | |
| User-entered climate only | No external climate source. | |

**User's choice:** Google AI Studio for AI; define the climate contract now and choose its provider before production.
**Notes:** Google AI Studio supplies access to Gemini API, not weather data. The official C# package is `Google.GenAI`; keep its key server-side.

---

## Crop catalog and agronomic rules

| Option | Description | Selected |
|--------|-------------|----------|
| Plátano, yuca, batata | Start with the most prominent crops named in the local interview. | |
| Include arroz and habichuela | Include all five crops in the initial catalog. | ✓ |
| Configurable catalog | Support administrator-managed crops from the beginning. | |
| Show missing parameters | Do not invent agronomic values when validated data is absent. | ✓ |
| Allow labeled AI estimates | Produce estimates while visibly marking them as unverified. | |
| Only activate crops with validated parameters | Hide or disable crop capabilities until their data is approved. | |

**User's choice:** Include arroz and habichuela; show missing agronomic data and never invent parameters.
**Notes:** The initial catalog contains plátano, yuca, batata, arroz, and habichuela.

---

## Manual logbook and account recovery

| Option | Description | Selected |
|--------|-------------|----------|
| Immutable correction | Create a linked correction operation and preserve the original entry. | ✓ |
| In-place edit plus separate audit | Update the row and record a separate audit event. | |
| Support-mediated account recovery | Email code, support review, and temporary password-change mechanism. | ✓ |
| Automatic email reset | User resets the password without support review. | |
| Defer recovery | Exclude account recovery from the pilot. | |

**User's choice:** Immutable logbook corrections and support-mediated account recovery.
**Notes:** Keep code lifetime, attempt limits, and session revocation policy to phase planning.

---

## the agent's Discretion

- Select bearer and refresh-token details that fit the .NET API clients without trusting farm-scoped token claims.
- Select .NET data-access/migration tooling and integration timeout/retry details during research and planning.

## Deferred Ideas

- Worker, advisor, offline sync, marketplace, transport, and field-service details remain assigned to later roadmap phases.
