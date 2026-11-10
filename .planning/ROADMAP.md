# Roadmap: AgroPlan Backend

## Overview

Deliver AgroPlan's farm-scoped backend pilot first, then expand into farm operations, worker coordination, AI-assisted field intelligence, marketplace and traceability, and transport. The API and Worker use ASP.NET Core/.NET, Supabase PostgreSQL and Storage, CloudAMQP RabbitMQ, and SignalR. Client-owned interface requirements are represented as backend integration capabilities where they apply; visual UI work stays in client repositories.

## Phases

**Phase Numbering:** Integer phases (1, 2, 3) are planned work.

- [x] **Phase 1: Farm Planning Pilot** - Producers can manage core farm data, review planting assistance, and keep a manual logbook. (completed 2026-11-10)
- [x] **Phase 2: Farm Operations and Safety** - Producers can record and analyze operating costs, inputs, irrigation, harvest, calendars, and authorized advisory access. (completed 2026-11-10)
- [ ] **Phase 3: Workforce and Offline Operations** - Workers and administrators can coordinate tasks, evidence, verification, and reliable offline synchronization.
- [ ] **Phase 4: Applied Farm Intelligence** - Producers can use voice drafts, weekly summaries, and future image-based agronomy reports with human review.
- [ ] **Phase 5: Marketplace and Traceability** - Producers and buyers can transact lots and verify origin and operation history.
- [ ] **Phase 6: Transport and Field-Service Integrations** - Sales can connect to freight workflows, and backend services support accessible, low-bandwidth client experiences.

## Phase Details

### Phase 1: Farm Planning Pilot
**Goal**: A producer can securely create an account, set up a farm and crop cycle, use planting assistance, and keep a manual logbook.
**Depends on**: Nothing (first phase)
**Requirements**: REQ-pilot-accounts, REQ-pilot-farms-parcels, REQ-pilot-crop-cycle, REQ-pilot-sowing-plan, REQ-pilot-planting-frame, REQ-pilot-manual-logbook, REQ-pilot-test-suite, REQ-pilot-api-docs, REQ-pilot-provider-neutral-ai, REQ-rf-g-01, REQ-rf-g-05, REQ-rf-g-10, REQ-rf-g-11, REQ-rf-g-38
**Success Criteria** (what must be TRUE):
  1. A producer can create and access an account, create a farm and parcels, and associate a crop cycle with a parcel; users outside that farm cannot read or change its records.
  2. A producer can request a sowing proposal, inspect climate availability or missing-data status plus its factors and uncertainty, and explicitly confirm only a complete, supported proposal; live climate and numeric agronomic advice stay disabled until their sources are selected and validated.
  3. A producer can request a farm-scoped planting-frame proposal. When validated spacing rules and usable parcel geometry are available, capacity is calculated deterministically and the producer explicitly confirms a choice; otherwise the API names the missing inputs and returns no capacity or options. Numeric comparison stays gated until agronomic sources and geometry are validated.
  4. An authorized producer can add and correct manual logbook entries while preserving the correction history.
**Plans:** 12/12 plans complete

Plans:
- [x] 01-01-PLAN.md — .NET skeleton and owner farm tracer
- [x] 01-02-PLAN.md — Account sessions and support-mediated recovery
- [x] 01-03-PLAN.md — Farm administration and parcel management
- [x] 01-04-PLAN.md — Five-crop catalog and farm crop cycles
- [x] 01-05-PLAN.md — Durable async sowing job and outbox lifecycle
- [x] 01-06-PLAN.md — Gemini/climate adapters and proposal review
- [x] 01-07-PLAN.md — Deterministic planting-frame proposals and confirmation
- [x] 01-08-PLAN.md — Immutable logbook and optional photo uploads
- [x] 01-09-PLAN.md — Provider-neutral AI contract and Scalar API reference
- [x] 01-10-PLAN.md — xUnit v3 unit test suite for pilot rules and worker behavior
- [x] 01-11-PLAN.md — API, persistence, and worker integration test coverage
- [x] 01-12-PLAN.md — Provider-neutral AI validation and producer setup E2E gap closure

### Phase 2: Farm Operations and Safety
**Goal**: A producer can maintain operational records and use them to make safer, better-informed farm decisions.
**Depends on**: Phase 1
**Requirements**: REQ-rf-g-02, REQ-rf-g-03, REQ-rf-g-04, REQ-rf-g-07, REQ-rf-g-08, REQ-rf-g-13, REQ-rf-g-15, REQ-rf-g-31, REQ-rf-g-32, REQ-rf-g-33, REQ-rf-g-34, REQ-rf-g-35, REQ-rf-g-36, REQ-rf-g-37, REQ-rf-g-42
**Success Criteria** (what must be TRUE):
  1. A producer can record expenses, labor/material usage, irrigation, input applications, and harvest data against the relevant farm, crop, and cycle.
  2. Cost and harvest reports use recorded units and source data, and a harvest is blocked or flagged until an input's documented withholding period has elapsed.
  3. A producer can view a crop calendar and reference prices, and receive climate or stage-related advisories with their source and validity.
  4. A producer can grant an advisor explicit, scoped, time-bound access and revoke it; the advisor can only access authorized farm data and reports.
**Plans**: 8/8 plans complete
- [x] 02-01-PLAN.md
- [x] 02-02-PLAN.md
- [x] 02-03-PLAN.md
- [x] 02-04-PLAN.md
- [x] 02-05-PLAN.md
- [x] 02-06-PLAN.md
- [x] 02-07-PLAN.md
- [x] 02-08-PLAN.md — Deterministic schedule proposals and provider-neutral AI explanations

### Phase 3: Workforce and Offline Operations
**Goal**: Farm administrators and workers can coordinate verified field tasks and safely synchronize changes after reconnecting.
**Depends on**: Phase 2
**Requirements**: REQ-rf-g-06, REQ-rf-g-09, REQ-rf-g-12, REQ-rf-g-14, REQ-rf-g-17, REQ-rf-g-18, REQ-rf-g-19, REQ-rf-g-20, REQ-rf-g-21, REQ-rf-g-22, REQ-rf-g-23, REQ-rf-g-25, REQ-rf-g-26, REQ-rf-g-27, REQ-rf-g-28, REQ-rf-g-29, REQ-rf-g-30, REQ-rf-g-41
**Success Criteria** (what must be TRUE):
  1. An administrator can invite workers, assign daily tasks, receive evidence, and approve, reject with a reason, or request a correction.
  2. Workers can access their task history and verified evidence; configured rewards and team progress reflect only verified work.
  3. A worker can record task changes offline and synchronize them later without duplicate effects; concurrent edits are surfaced as conflicts instead of silently overwriting either version.
  4. Confirmed field records update affected tasks and calendars with an explanation, while independent operations continue when unrelated dependencies fail.
**Plans**: 4/7 plans executed
**Wave 1**
- [x] 03-01-PLAN.md

**Wave 2** *(blocked on Wave 1 completion)*
- [x] 03-02-PLAN.md

**Wave 3** *(blocked on Wave 2 completion)*
- [x] 03-03-PLAN.md
- [x] 03-04-PLAN.md
- [ ] 03-05-PLAN.md
- [ ] 03-06-PLAN.md

**Wave 4** *(blocked on Wave 3 completion)*
- [ ] 03-07-PLAN.md

### Phase 4: Applied Farm Intelligence
**Goal**: Producers can use AI-assisted field inputs and summaries while retaining control over every business record and agronomic decision.
**Depends on**: Phase 3
**Requirements**: REQ-rf-g-39, REQ-rf-g-40, REQ-rf-g-43
**Success Criteria** (what must be TRUE):
  1. A voice recording can produce an editable activity draft with missing fields identified, and no logbook record is created until the user confirms it.
  2. A weekly summary is based on confirmed records and pending tasks, identifies missing data, and lets the user trace calculated totals to their source records.
  3. A photo-based symptoms report is clearly advisory and requires agronomist confirmation before presenting a diagnosis or treatment as approved.
**Plans**: TBD

### Phase 5: Marketplace and Traceability
**Goal**: Producers and buyers can publish, discover, negotiate, and complete produce transactions with accurate lot availability and evidence-based traceability.
**Depends on**: Phase 4
**Requirements**: REQ-rf-g-16, REQ-rf-m-01, REQ-rf-m-02, REQ-rf-m-03, REQ-rf-m-04, REQ-rf-m-05, REQ-rf-m-06, REQ-rf-m-07, REQ-rf-m-08, REQ-rf-m-09, REQ-rf-m-10, REQ-rf-m-15, REQ-rf-m-16, REQ-rf-m-17, REQ-rf-m-18, REQ-rf-m-19, REQ-rf-m-20, REQ-rf-m-21, REQ-rf-m-22, REQ-rf-m-23, REQ-rf-m-24, REQ-rf-m-25, REQ-rf-m-26, REQ-rf-m-27, REQ-rf-m-28
**Success Criteria** (what must be TRUE):
  1. A producer can publish and update a lot, a buyer can find it, and the selected sale mode's quantity and price rules are enforced for every open offer and agreement.
  2. A completed agreement releases contacts and creates an order with the agreed quantity, price, delivery date, and place; delivery can be confirmed by both parties.
  3. A QR pass exposes only approved origin and verification evidence and never claims produce is chemical-free without supporting evidence.
  4. Ratings, public profiles, private invitations, and AI-prepared listing drafts respect their visibility rules and require explicit seller approval before publication.
**Plans**: TBD

### Phase 6: Transport and Field-Service Integrations
**Goal**: Completed sales can connect to freight workflows, and backend interfaces support field clients operating with limited connectivity and assistive input.
**Depends on**: Phase 5
**Requirements**: REQ-rf-g-24, REQ-rf-m-11, REQ-rf-m-12, REQ-rf-m-13, REQ-rf-m-14, REQ-rf-t-01, REQ-rf-t-02, REQ-rf-t-03, REQ-rf-t-04, REQ-rf-t-05, REQ-rf-t-06
**Success Criteria** (what must be TRUE):
  1. Closing a sale can trigger a freight request, verified transporters can submit quotes, and an authorized party can select a quote and view basic trip progress.
  2. Backend responses and event contracts support concise, low-bandwidth field workflows and voice-assisted entry; client-specific visual design remains in client repositories.
  3. Important events can be delivered through configured application, WhatsApp, and SMS integrations, with provider and delivery state visible to the client.
  4. The backend exposes the data and endpoints required for accessible workflows and brief in-app training content without coupling server logic to a specific client UI.
**Plans**: TBD

## Progress

**Execution Order:** Phases execute in numeric order.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Farm Planning Pilot | 12/12 | Complete    | 2026-11-10 |
| 2. Farm Operations and Safety | 8/8 | Complete    | 2026-11-10 |
| 3. Workforce and Offline Operations | 4/7 | In Progress | - |
| 4. Applied Farm Intelligence | 0/TBD | Not started | - |
| 5. Marketplace and Traceability | 0/TBD | Not started | - |
| 6. Transport and Field-Service Integrations | 0/TBD | Not started | - |
