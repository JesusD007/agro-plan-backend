# Phase 2 Multi-Source Planning Audit

| Source | ID | Covered item | Plan | Status |
|---|---|---|---|---|
| GOAL | — | Maintain operational records and use evidence for safer, better-informed decisions | 02-01 through 02-08 | COVERED |
| REQ | REQ-rf-g-02 | Dated crop-cycle expenses, labor and materials | 02-01 | COVERED |
| REQ | REQ-rf-g-03 | Cost per quintal from sourced costs and harvest quantity | 02-06 | COVERED with explicit unavailable result when conversion/quintal rules are missing |
| REQ | REQ-rf-g-04 | Validated standard crop task calendar | 02-05 | COVERED with missing-configuration state where rules are absent |
| REQ | REQ-rf-g-07 | Harvest blocked until exact applicable validated PHI elapses | 02-04 | COVERED |
| REQ | REQ-rf-g-08 | Reference prices with source, date, unit, currency, channel and market | 02-03 | COVERED with unavailable/stale state until source approval |
| REQ | REQ-rf-g-13 | Cycle calendar progress and deterministic schedule changes with AI explanation and producer confirmation | 02-05, 02-08 | COVERED |
| REQ | REQ-rf-g-15 | Exportable cost and yield reports | 02-06 | COVERED |
| REQ | REQ-rf-g-31 | Irrigation records with method, source and date | 02-02 | COVERED |
| REQ | REQ-rf-g-32 | Structured input applications, PHI and validated application-frequency assessment; missing frequency rules return unavailable | 02-02, 02-04 | COVERED |
| REQ | REQ-rf-g-33 | Stage guidance with validated provenance and advisory disclaimer | 02-05 | COVERED with missing-configuration state where rules are absent |
| REQ | REQ-rf-g-34 | Stage reminders backed by configured parameters | 02-05 | COVERED with missing-configuration state where rules are absent |
| REQ | REQ-rf-g-35 | Weather hazard alerts with geography, source and validity | 02-03 | COVERED with unavailable/stale state until source approval |
| REQ | REQ-rf-g-36 | Exportable crop, planted area/date and harvested quantity report | 02-06 | COVERED |
| REQ | REQ-rf-g-37 | Producer-authorized advisor access to scoped farm data and reports | 02-07 | COVERED |
| REQ | REQ-rf-g-42 | Evidence-backed harvest window/actions from planting inputs, validated parameters and confirmed progress; confirmed actual harvest remains PHI-gated | 02-04, 02-05, 02-08 | COVERED: sourced estimate/actions when evidence is current; explicit unavailable/missing-input state otherwise; actual registration blocked until applicable validated PHI elapses |
| RESEARCH | — | .NET 10, ASP.NET Core, EF Core/PostgreSQL, farm-scoped queries and composite farm keys | 02-01 through 02-08 | COVERED |
| RESEARCH | — | xUnit v3 unit, integration, security, and provider-neutral contract tests | 02-01 through 02-08 | COVERED |
| RESEARCH | — | Immutable operation corrections; exact source/version provenance; original units/currency | 02-01, 02-02, 02-04, 02-06 | COVERED |
| RESEARCH | — | No validated PHI, calendar/frequency thresholds, quintal equivalence or unit conversions supplied | 02-02, 02-04, 02-05, 02-06 | COVERED by deterministic safe-unavailable behavior |
| RESEARCH | — | Climate and price feed source/freshness remain unselected | 02-03 | COVERED as unresolved production gates; no provider selected |
| RESEARCH | — | Advisor grant revocation and scope checked against current persisted state | 02-07 | COVERED |
| CONTEXT | D-01 | Missing or inapplicable validated PHI blocks harvest registration/eligibility | 02-04 | COVERED |
| CONTEXT | D-02 | Deterministic, traceable PHI; distinguish PHI, MRL and re-entry interval | 02-04 | COVERED |
| CONTEXT | D-03 | No unsupported agronomic, climate or stage values | 02-03, 02-05 | COVERED |
| CONTEXT | D-04 | Advisor access read-only, farm/category scoped, bounded, revocable, checked each request | 02-07 | COVERED |
| CONTEXT | D-05 | Advisor grant distinct from membership; no account-wide/edit access | 02-07 | COVERED |
| CONTEXT | D-06 | Confirmed operational records immutable; corrections append linked actor/time/reason | 02-01, 02-02, 02-04 | COVERED |
| CONTEXT | D-07 | Keep entered units; convert only by sourced explicit rules | 02-01, 02-02, 02-06 | COVERED |
| CONTEXT | D-08 | Currency on every amount; no silent mixed-currency conversion/aggregation | 02-01, 02-06 | COVERED |
| CONTEXT | D-09 | Reports derive from farm/cycle records and identify source rows | 02-06 | COVERED |
| CONTEXT | D-10 | Climate/price source and freshness metadata; no-data is not no-risk/zero-price | 02-03 | COVERED |
| CONTEXT | D-11 | Calendars/guidance/harvest windows sourced, validated and effective-dated | 02-04, 02-05, 02-08 | COVERED: estimate provenance and missing-input states are explicit; actual harvest remains independently PHI-gated |
| CONTEXT | D-12 | PostgreSQL truth; synchronous ordinary writes; durable async external scheduling | 02-01 through 02-08 | COVERED |

## Excluded by Scope

Offline sync and worker task assignment (Phase 3); voice, weekly AI summaries and image agronomy (Phase 4); marketplace, transport, equipment and client UI are not planned here. No climate or market price provider is selected. Safe unavailable states satisfy this phase's data-integrity contract; enabling live outputs remains a production gate.
