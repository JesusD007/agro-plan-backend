# Phase 2 Domain Research: Farm Operations and Safety

**Research date:** 2026-11-10  
**Domain:** Dominican Republic smallholder and commercial farm operations  
**Scope:** Traceable farm costs and production, input-application safety, harvest eligibility, crop calendars/advisories, reference prices, and producer-authorized advisor access.

## Domain Findings

### 1. Pesticide use and pre-harvest interval (PHI)

In the Dominican Republic, the Ministry of Agriculture describes the approved pesticide label and leaflet as the controlling use instructions, including the crop/use, application method/rate, frequency, re-entry interval, and pre-harvest interval. Its product-registration service requires the label and leaflet as part of registration. FAO guidance also treats the PHI as a product/use-label-specific interval between the last treatment and harvest, with traceability of the product and actual application.

**Safe product behavior:**

- Record the commercial product, active ingredient(s), formulation, label/registration identifier and version, crop/use, application date and time, treated parcel/cycle, application method (including aerial or drone application), applied amount and unit, area, and supporting label/evidence reference.
- Keep the pre-harvest interval (PHI) distinct from the re-entry interval (REI). One is a harvest restriction; the other concerns people entering treated fields.
- Derive a harvest hold only from a verified label that matches the registered product/formulation, crop/use, and recorded application. For multiple applicable treatments, use the latest applicable release date only when each label and application is verified.
- If product identity, crop-use match, label version, date, or PHI is missing/ambiguous, return `unknown`/`requires review` and prevent the API from asserting that harvest is safe. A missing interval must never be treated as zero days.
- Preserve the label/evidence source and rule version used for every eligibility decision. Changes to a label or correction of an application record should invalidate/recalculate the decision with an audit trail.

The official Dominican pesticide-residue database is a separate source of maximum residue limits (MRLs), based on Decree 244-10 and Codex limits as a first reference. An MRL is not a PHI: do not calculate a waiting period from an MRL, active ingredient alone, generic crop advice, or a PHI shown for a different crop/use.

Fertilizer and other non-pesticide inputs should remain distinct categories. Do not assign a pesticide PHI to a fertilizer or infer one from nutrient composition. If a label or competent agronomist establishes a relevant harvest restriction, record its source and applicability explicitly.

### 2. Cost, labor, inputs, irrigation, and production traceability

The record is useful only when its quantity can be interpreted. Dominican agriculture publications use several crop-specific units: the Ministry's 2024 operational plan lists a quintal as 100 lb for the cited production consolidation and also presents units such as millar, racimo, paquete, paca, and fanega. It warns that example equivalences vary by product. Therefore a single universal “quintal” or conversion table is unsafe for every crop.

**Safe product behavior:**

- Store each observed quantity with its original unit, unit symbol/name, crop/product context, and source. Do not silently convert or discard the entered unit.
- Normalize only through an explicit, crop-appropriate conversion rule with provenance; expose both the recorded and normalized quantities in reports when conversion occurs.
- Store monetary amounts with an ISO currency code (for a Dominican farm record, DOP is a reasonable default), period, and source. Do not sum mixed currencies without an explicit exchange rate, rate date/source, and visible conversion.
- Separate actual recorded costs and harvest quantities from reference prices, estimates, and forecast values. Reports should identify the records and conversion rules behind each total.
- Calculate production cost per unit only when the numerator's cost scope and denominator's harvest quantity/unit are both known and compatible. Otherwise show the missing data or omit that metric.
- Corrected operational entries should retain the original value and append an attributed correction, consistent with Phase 1's immutable-history pattern.

### 3. Reference prices

The Ministry of Agriculture publishes agricultural price series for distinct channels, including farm-gate/producer, wholesale, retail, and inter-daily market observations. Those are different market observations, not interchangeable “the market price.” The source pages and files carry observation periods or update dates.

**Safe product behavior:** show commodity/variety, region or market, channel (producer/wholesale/retail), unit, currency, observed/published date, source, and dataset freshness. Label the value as a reference observation; do not present it as the producer's realized sale price, guaranteed offer, or a current quote after its validity window.

### 4. Weather and crop-stage advisories

The Dominican Republic's meteorological authority is now INDOMET, created by Law 19-24 (the institution's official pages also explain that bulletins are available through its portal). INDOMET bulletins distinguish forecasts from official alerts and define geographic areas and periods of impact. The COE may publish operational emergency alerts; hydrological conditions can have a separate source such as INDRHI.

**Safe product behavior:** store publisher, bulletin/source URL or identifier, issue time, start/end of the forecast or alert, affected province/area, hazard, units, and retrieval time. Display the source and validity with every advisory. Once the source validity expires, mark the advisory stale/unavailable; absence of a fresh bulletin must not be shown as “no risk.” Use official weather facts as weather facts, and do not infer crop-specific action thresholds without a validated agronomic rule.

For crop calendars or stage guidance, every task/threshold should identify crop, region/cultivation assumptions, source, validation status, effective dates, and revision. Confirmed farm work can update actual progress; an AI or rules engine may propose a schedule change, but a producer must confirm the change as the roadmap requires. Until a crop-stage rule is validated, show the configured task data or the missing-data status rather than invent dates or fertilizer rates.

### 5. Advisor access and farm data

Phase 2 requires an explicit producer grant that is limited by farm, scope, and time, and can be revoked. The Dominican Republic's Law 172-13 applies to personal data in public and private databases and recognizes data-subject access/correction rights and privacy/security principles. This is relevant when farm records include identifiable people or are shared with an advisor; local counsel should review final notices, retention, and access workflows before production.

**Safe product behavior:** default an advisor grant to read-only and least privilege; identify the specific farm and allowed record/report categories, start/end, granting producer, and purpose; log grant, use, expiry, and revocation; check current grant state on every request so revocation takes effect immediately. Do not turn a farm-scoped grant into account-wide access or include records outside the explicit scope in exports. Do not expose personal contact data unless that data is necessary and explicitly within the grant.

## Domain-Specific Failure Modes

1. **Wrong label or crop-use match:** same active ingredient can have different PHIs by product, formulation, crop, rate, or jurisdiction. A lookup by active ingredient alone may clear harvest incorrectly.
2. **Unit mismatch in reports:** combining lb, kg, fanega, racimo, millar, tarea, or hectare values without a verified conversion can produce plausible but materially wrong yield or cost metrics.
3. **Stale or geographically mismatched advisory:** a correct bulletin can still be unsafe if it is expired, applies to a different province, or forecast period does not include the planned task.
4. **Reference price mistaken for realized farm revenue:** source channel, commodity variety, location, and date materially change the meaning of the number.
5. **Advisor permission outlives consent:** a cached token or broad export can preserve access after a producer revokes the farm/scope grant.

## Domain Evaluation Criteria

| Dimension | Good (expert accepts) | Bad (expert flags) | Stakes |
|---|---|---|---|
| Harvest hold traceability | Decision names the exact registered product/label, crop use, last application, PHI source, and computed earliest date; unknown fields produce a hold/review state. | Says harvest is safe from generic ingredient data, an MRL value, missing input treated as zero, or an unrelated crop label. | Critical |
| Operational quantity integrity | Cost and yield totals can be traced to dated source records and retain original units; conversions name their crop-specific rule. | Reports a cost/quintal or yield total without establishing which quintal/unit, conversion, or underlying records were used. | High |
| Price reference meaning | Price is labeled by market channel, market/region, crop/variety, unit, currency, source, and observation date. | A stale retail/wholesale value is presented as a guaranteed farm-gate price or realized income. | High |
| Weather advisory validity | Advisory cites INDOMET/COE source, geographic scope, hazard, issue/validity window, and clearly expires. | Old or province-mismatched warning is presented as current; no-data is presented as safe weather. | High |
| Advisor scope and revocation | Advisor can read only explicitly granted farm/report categories until the grant expires or is revoked; the next request is denied after revocation. | Advisor sees other farms, has unapproved edit/export access, or retains access after revocation. | Critical |

## Domain Expert Roles

| Role | Responsibility |
|---|---|
| Dominican agronomist/extensionist familiar with the target crops | Validate crop-stage calendars, unit conventions, data completeness, and the meaning of safe-mode outputs. |
| Ministry of Agriculture pesticide registration/plant-health specialist | Confirm which official label/registration artifact controls the crop-use and PHI data model. |
| Food-safety or residue specialist | Review harvest-hold logic and distinguish PHIs from MRLs and residue test results. |
| Agricultural economist or farm-accounting practitioner | Calibrate cost categories, harvest units, cost-per-unit calculations, and producer/wholesale/retail price interpretation. |
| INDOMET/COE information specialist | Check weather source attribution, geographic coverage, bulletin validity, and alert wording. |
| Producer and authorized advisor | Review least-privilege scope, usefulness of reports, grant/expiry/revocation clarity, and privacy expectations. |

## Planning Implications

- No crop-specific PHI, pesticide dose/frequency, fertilizer rate, calendar task date, climate threshold, or unit conversion is supplied by this research. Those require verified label, official data, and/or local expert approval.
- Climate remains unconfigured in Phase 1. Phase 2 may model source/validity metadata and safe unavailable states; production advisories need a selected provider and operational freshness policy.
- High-risk harvest calculations should be deterministic, sourced, versioned, and tested independently of any AI. AI may explain records or propose schedule adjustments but must not establish chemical safety or override an unresolved hold.
- The exact business rule for whether an unverified/unknown PHI blocks harvest confirmation or permits only an explicitly flagged manual record remains a product decision for discussion. The safe default is to prevent the system from marking the harvest eligible until reviewed.
- The exact default currency and any canonical reporting units should be confirmed. Preserve original producer-entered units regardless of any later normalized view.

## Sources

- [Ministerio de Agricultura — Registro de productos comerciales de plaguicidas agrícolas](https://agricultura.gob.do/servicios/registro-de-productos-comerciales-de-plaguicidas-agricolas/) — registration includes the label/leaflet for the intended product.
- [Ministerio de Agricultura — Manual para distribuidores de plaguicidas, Chapter 3](https://agricultura.gob.do/transparencia/wp-content/uploads/2023/02/Manual-Distribuidores-de-Plaguicidas-2da-Edicion.pdf) — label's legal role and fields including crop/use, re-entry, and pre-harvest interval.
- [FAO — Pesticide application and PHI/traceability](https://www.fao.org/4/i1645e/i1645e00.pdf) — PHI is label-specified; application records support traceability.
- [FAO — JMPR data requirements and GAP table](https://www.fao.org/4/x5848e/X5848e05.htm) — PHI should come from explicit approved-label statements for the relevant commodity/use.
- [Ministerio de Agricultura — Dominican pesticide MRL database](https://inocuidad.agricultura.gob.do/) — national MRL data and relation to Decree 244-10/Codex; MRLs are not PHIs.
- [Ministerio de Agricultura — POA 2024](https://agricultura.gob.do/transparencia/wp-content/uploads/2024/01/MARD_FINAL-POA-2024_REAJUSTADO-en-PDF_Jun-14-2024.pdf) — official production units/equivalences with crop-dependent examples.
- [Ministerio de Agricultura — producer price statistics](https://agricultura.gob.do/category/estadisticas-agropecuarias/precios-de-productos-agropecuarios/precios-del-productor/) and [inter-daily price reports](https://agricultura.gob.do/informe-de-precios-interdiario/) — distinct price channels and dated observations.
- [INDOMET — official forecast portal](https://indomet.gob.do/) and [Law 19-24 creating INDOMET](https://wp.indomet.gob.do/wp-content/uploads/2024/08/LEY-19-24-QUE-CREA-EL-INSTITUTO-DOMINICANO-DE-METEOROLOGA-INDOMET.pdf) — current Dominican meteorological authority and alert/forecast mandate.
- [Presidencia de la República — Law 172-13](https://presidencia.gob.do/sites/default/files/statics/transparencia/marco-legal/leyes/Ley-172-13.pdf) — Dominican personal-data protection law; product-specific application should be reviewed by counsel.
