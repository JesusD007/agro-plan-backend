---
phase: 02-farm-operations-and-safety
audited: 2026-11-10
status: secured
asvs_level: 1
blocking_threshold: high
threats_closed: 23
threats_open: 0
---

# Phase 2 Security Audit

**Verdict:** SECURED — all 23 declared threat entries are closed; no unregistered threat flags were reported.

## Findings

| Threats | Status | Evidence |
|---|---|---|
| T-02-01, T-02-02 | CLOSED | Expense routes recheck active farm membership and filter by farm; same-farm references and PostgreSQL immutability guards protect records; corrections append linked entries. |
| T-02-04, T-02-05 | CLOSED | Irrigation/application routes are farm-scoped; correction history is immutable; client requests cannot set PHI. |
| T-02-06, T-02-07 | CLOSED | Weather and price providers preserve provenance and freshness/market metadata; unconfigured providers return unavailable. |
| T-02-08, T-02-09 | CLOSED | Harvest requires one exact, effective, validated product/formulation/crop/use/jurisdiction label; missing PHI blocks registration; current evidence is rechecked. |
| T-02-10 | CLOSED | Calendar guidance requires current validated crop and geographic evidence; missing evidence yields unavailable. |
| T-02-12, T-02-13 | CLOSED | Reports are farm-filtered, CSV formula values are escaped, corrections are resolved before date filtering, and currencies/units remain grouped unless conversion is supported. |
| T-02-14, T-02-15 | CLOSED | Advisor grants are farm/scope/time limited, revocable, read-only, and checked against current state on each request. |
| T-02-16 | CLOSED | Schedule dates are deterministic; producer confirmation rechecks membership, evidence and proposal versions; AI explanations are validated and non-authoritative. |
| T-02-17 | CLOSED | Gemini receives positional task labels, a generic rule version and semantic evidence labels; GUIDs, internal task keys and source versions remain server-side. An integration test asserts identifiers are absent. |
| T-02-SC (all 8 plans) | CLOSED | No package or dependency manifest changed; no new package was added during Phase 2. |

## Production Gates

Live weather, reference-price and agronomic/PHI sources remain unselected or unvalidated. Their providers stay unavailable and safety checks fail closed until the sources and validation process are approved. Live Gemini activation remains subject to the evaluation, privacy, retention, monitoring and disable-control requirements in `02-AI-SPEC.md`.

---

_Audited with GSD security-auditor on 2026-11-10._
