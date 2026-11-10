# Phase 2: Farm Operations and Safety - Discussion Log

> Audit trail only. Decisions are in 02-CONTEXT.md.

**Date:** 2026-11-10
**Phase:** 2-farm-operations-and-safety
**Areas discussed:** Harvest safety, Advisor access, Units and reporting, Operational corrections

## Harvest safety

| Option | Description | Selected |
|---|---|---|
| Block if missing | Block harvest when validated withholding evidence is missing. | ✓ |
| Allow with warning | Record harvest with unresolved safety warning. | |
| Manual review | Hold for an authorized manual decision. | |

**User's choice:** Block if missing (Recommended)
**Notes:** Missing/mismatched evidence never means zero days.

## Advisor access

| Option | Description | Selected |
|---|---|---|
| Read-only with scope | Access per farm/data category, with expiry and revocation. | ✓ |
| Read and edit | Advisor can update operations. | |
| Aggregated reports | Advisor sees aggregates only. | |

**User's choice:** Solo lectura con alcance (Recommended)
**Notes:** Recheck scope and grant state on every request.

## Units and reporting

| Option | Description | Selected |
|---|---|---|
| Original unit + explicit conversion | Keep entered unit; convert only through a sourced rule. | ✓ |
| Normalize to quintals and DOP | Convert all values to one unit/currency. | |
| Choose per report | Report caller chooses output unit. | |

**User's choice:** Unidad original + conversión explícita (Recommended)
**Notes:** Do not aggregate incompatible units or silently convert currencies.

## Operational record corrections

| Option | Description | Selected |
|---|---|---|
| Immutable linked corrections | Preserve originals and append linked corrections. | ✓ |
| Editable with history | Update with version snapshots. | |
| Editable without history | Update/delete original. | |

**User's choice:** Inmutables y enlazadas (Recommended)
**Notes:** Preserve actor, time, reason, and prior values.

## the agent's Discretion

- No currency default was selected; retain currency codes and avoid mixed-currency totals without explicit conversion.
- No climate or reference-price source was selected; report unavailable/stale data until configured.
- CSV is a reasonable first export format unless later client requirements specify otherwise.

## Deferred Ideas

- Offline synchronization and worker workflows (Phase 3).
- Voice/weekly AI and image-based agronomy (Phase 4).
- Marketplace and transport in later phases.

