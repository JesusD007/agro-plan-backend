# Phase 3 Domain Research: Workforce and Offline Operations

**Date:** 2026-11-10
**Domain:** Smallholder and family-farm field workforce coordination in the Dominican Republic

## Domain signal

- **Users:** Farm administrators and owners, permanent and seasonal workers, and field operations advisers who may support task setup or review.
- **Stakes:** High for task evidence, wage records, privacy, access revocation, and conflict resolution; medium for points, badges, and route ordering. A wrong record can affect pay disputes, farm operations, or worker safety.
- **Output consequence:** The system records assignment, submission, review, verified work, route version, and sync outcome. These records can influence what work is done, what evidence is retained, and what wages/rewards the administrator acknowledges. AgroPlan does not transfer wages.
- **Operating context:** Agricultural work is seasonal and often conducted in rural areas where connectivity is intermittent. The functional specification also sets low-bandwidth, low-end-device, offline consultation and recording as design constraints.

## What domain practitioners should evaluate

### 1. A work record can be trusted and contested fairly

- **Good:** Each assignment shows who assigned it, the agreed task/date and any wage amount/unit; a worker submission is retained with its evidence and timestamp; an administrator decision and reason are attributable; corrections preserve the prior submission and create a linked revision; a worker can see the outcome and wage status.
- **Bad:** The system overwrites earlier evidence, attributes a submission to the wrong worker, turns an unverified completion into confirmed wages, or leaves a rejection/correction without a reason or trace.
- **Stakes:** Critical.
- **Source:** The user-selected Phase 3 decisions and actor flow A5 require administrator verification, retained evidence, and verified work before wage/point credit. ILO guidance notes that piece-rate arrangements need transparency and should reward work fairly; see [ILO, Piece rate pay](https://www.ilo.org/resource/17-piece-rate-pay) and [ILO, Business, wages and benefits](https://www.ilo.org/ilo-helpdesk-questions-and-answers-business-wages-and-benefits).

### 2. Offline work is preserved without duplicate or silent replacement

- **Good:** A retry with the same client operation ID returns its original outcome without applying the operation twice; a batch reports each result; independent operations proceed when unrelated work fails; stale task versions remain available as a conflict until an administrator chooses a resolution.
- **Bad:** A reconnect duplicates verified work or wages, silently drops a local task change, resolves a conflict by last-write-wins, or blocks all unrelated sync operations after one invalid operation.
- **Stakes:** Critical.
- **Source:** Functional actor flow A2, requirements RNF-01 and RNF-03, and the selected Phase 3 sync policy. FAO describes weak connectivity in rural farm environments as a barrier to reliable digital agriculture and field data transfer; see [FAO, Digital agriculture in action](https://assets.nena.fao.org/public/2022-10/digital_agriculture_in_action_artificial_intelligence_for_agriculture.pdf) and [FAO, Ground offline data collection](https://www.fao.org/in-action/forest-data-partnership/activities/solutions/2/en).

### 3. Worker monitoring is limited, visible, and consensual

- **Good:** Location/time are off unless the worker consents for the relevant task; the purpose and scope are clear; evidence is only visible to farm-authorized users with a task-related need; worker history is not exposed publicly by default.
- **Bad:** The API silently captures continuous location, exposes a worker's personal history to other workers, or reuses task photos/location for unrelated scoring or public display.
- **Stakes:** High.
- **Source:** Phase 3 D-03 and the functional privacy requirements; ILO worker personal-data guidance emphasizes advance notice and explicit consent when collecting personal data from third parties or monitoring workers. See [ILO, Protection of workers’ personal data](https://www.ilo.org/legacy/english/intserv/working-papers/wp062/index.html). This source is general guidance, not a jurisdiction-specific legal determination.

### 4. Wage amounts and units are understandable, and verification is not payment

- **Good:** The worker and administrator can identify the agreed amount and unit from assignment through verification; the system clearly distinguishes proposed, submitted, verified, and paid/settled states (AgroPlan only owns the first three); corrections to assignment terms are auditable.
- **Bad:** The system labels a verified task as “paid,” changes the amount after work without history, or compares/aggregates different wage units as if equivalent.
- **Stakes:** Critical.
- **Source:** User decision D-05 and prior Phase 2 unit/currency rules; ILO guidance stresses transparent calculation, records, and clarity for piece-rate work. See [ILO, Piece rate pay](https://www.ilo.org/resource/17-piece-rate-pay) and [ILO, Business, wages and benefits](https://www.ilo.org/ilo-helpdesk-questions-and-answers-business-wages-and-benefits). National minimum wage and labor-law compliance require Dominican Republic legal review; this research does not establish the applicable rule for any employment relationship.

### 5. Points and badges recognize verified work without unsafe pressure

- **Good:** Points accrue only after administrator verification and from objective, disclosed, per-task criteria; rankings stay disabled unless explicitly enabled by the farm; photo clarity is not algorithmically scored; badge criteria are knowable and corrections can reverse or amend downstream totals transparently.
- **Bad:** Points are awarded for unverified work, speed alone, undocumented subjective judgments, or location/photo surveillance; a ranking exposes worker performance without a farm decision.
- **Stakes:** High for wage-linked or surveillance-adjacent effects; Medium for optional badges.
- **Source:** Functional sections 6.5 and actor flow A5 plus user decisions D-09 through D-11. ILO policy guidelines warn that piece-rate and seasonal work can encourage long hours and affect worker health/work-life balance; see [ILO, Policy guidelines for the promotion of decent work in the agri-food sector](https://www.ilo.org/sites/default/files/2024-05/Policy%20guidelines%20for%20the%20promotion%20of%20decent%20work%20in%20the%20agri-food%20sector.pdf).

### 6. Routes are current, reviewable, and do not overstate navigation precision

- **Good:** The route is attached to the authorized farm, selected tasks, and a version; users know when it is stale; an administrator explicitly reconfirms changes; offline data contains only the approved ordered stops and available geometry and is not presented as live navigation.
- **Bad:** A route remains marked current after task/parcel changes, a worker follows a route for a task they cannot access, or incomplete/old geometry is presented as safe turn-by-turn directions.
- **Stakes:** High.
- **Source:** Actor flow A6 says route provider/map use remain undecided and that routes must be available offline. User decisions D-12 through D-14 gate route generation on provider approval and require stale-version review. ILO agricultural safety standards emphasize control of workplace hazards and safety provisions for temporary and seasonal workers; see [ILO Convention 184 on Safety and Health in Agriculture](https://normlex.ilo.org/dyn/nrmlx_en/f?p=NORMLEXPUB:12100:0::NO::P12100_ILO_CODE:C184).

## Known domain failure modes

1. **Seasonal or temporary worker churn:** invitation expiry, cancellations, and plan caps can leave stale access behind or exclude a legitimate worker; revocation needs to be effective on server requests.
2. **Shared devices and intermittent connectivity:** work may be recorded under the wrong account or resubmitted after an uncertain network response; local pending records need clear ownership and idempotent retries.
3. **Photo evidence harms privacy or cannot prove the task:** images can include people, homes, or unrelated farm activity. A photo can document a submission but cannot by itself establish quality, correct location, or wage entitlement.
4. **Disagreement over completion or pay:** a manager's rejection, missing evidence, or changed wage terms can become contested. Preserve original assignment, worker submission, reviewer identity/reason, and later correction as distinct history.
5. **Gamification pushes unsafe behavior:** time/speed rankings or points for quantity may pressure workers to skip breaks or work unsafely. Keep rewards objective, optional, verified, and separate from wage settlement.
6. **Routes become stale or imprecise:** changed assignments and uncertain parcel geometry can lead to wasted trips or unsafe travel. A route contract must distinguish proposed/confirmed/stale and unavailable states; the provider and route safety assumptions require field validation.

## Regulatory and compliance context

- The product context is the Dominican Republic. This review did not identify or verify the applicable Dominican labor, privacy, or geolocation rules; before production use, obtain local legal review for worker data, consent, wage records, seasonal labor, retention, and any rankings.
- ILO Convention 184 and the ILO agri-food decent-work guidelines provide relevant international occupational-safety and work-time benchmarks. Their application to a specific Dominican employment relationship must not be inferred from this document.
- Treat GPS/location and timestamp as personal worker data: explain purpose, collect only when needed and consented, minimize retention, and restrict access. The Phase 3 API should not turn optional evidence metadata into continuous worker tracking.
- The phase records wage terms and verification, but does not make payments or determine legal wage compliance.

## Domain expert roles for review

| Role | Evaluation responsibility |
|------|---------------------------|
| Farm owner/administrator from the pilot area | Validate task setup, verification reasons, workload fit, invitation/cap policies, and practical wage units. |
| Seasonal/permanent farm worker, including women workers | Check consent language, offline usability, evidence burden, worker-visible history, fairness of disputes, and privacy expectations. |
| Dominican labor-law practitioner or labor inspector | Review wage records, employment/seasonal-work assumptions, consent, retention, and applicable legal obligations before production. |
| Agricultural occupational-safety practitioner | Review route information, task timing, breaks/safety signals, and any incentive/ranking criteria for unsafe pressure. |
| Cooperative or agricultural extension field agent | Review field terminology, low-connectivity workflow, shared-device patterns, and training/interpretation risks. |

## Suggested acceptance review scenarios

- Retry a worker's completion submission after a dropped response and confirm that the task, wage, and points are not duplicated.
- Submit a stale task edit after an administrator changed the schedule and confirm both versions remain visible and neither wins silently.
- Ask for correction and resubmit a new photo; verify the worker and administrator can inspect both evidence revisions and their decisions.
- Disable location consent and verify that no location is captured or required to finish an otherwise valid task.
- Verify a task and inspect wage status; confirm the API communicates recorded/verified status and never implies a payment happened.
- Change one task after route confirmation and confirm the prior route becomes stale, the worker is notified, and only an administrator-confirmed version is served as current.
- Fail a notification or external calendar event and confirm the task persists, the pending state is visible, retries are safe, and unrelated operations still complete.

## Research sources

- [FAO, Digital agriculture in action: Artificial intelligence for agriculture](https://assets.nena.fao.org/public/2022-10/digital_agriculture_in_action_artificial_intelligence_for_agriculture.pdf)
- [FAO, Ground offline data collection for remote agricultural and environmental work](https://www.fao.org/in-action/forest-data-partnership/activities/solutions/2/en)
- [ILO, Piece rate pay](https://www.ilo.org/resource/17-piece-rate-pay)
- [ILO, Business, wages and benefits](https://www.ilo.org/ilo-helpdesk-questions-and-answers-business-wages-and-benefits)
- [ILO, Protection of workers’ personal data: General principles](https://www.ilo.org/legacy/english/intserv/working-papers/wp062/index.html)
- [ILO, Policy guidelines for the promotion of decent work in the agri-food sector](https://www.ilo.org/sites/default/files/2024-05/Policy%20guidelines%20for%20the%20promotion%20of%20decent%20work%20in%20the%20agri-food%20sector.pdf)
- [ILO Convention 184: Safety and Health in Agriculture](https://normlex.ilo.org/dyn/nrmlx_en/f?p=NORMLEXPUB:12100:0::NO::P12100_ILO_CODE:C184)
- `../agro-plan-doc/Funcional/AgroPlan - Documentacion Funcional.md` §§4.1, 6.1, 6.4–6.6, 8.3, 14
- `../agro-plan-doc/Funcional/AgroPlan - Flujos de Datos por Actor y Función.md` §§A2, A5, A6
- `.planning/phases/03-workforce-and-offline-operations/03-CONTEXT.md`
