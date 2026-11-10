---
phase: "1"
slug: "farm-planning-pilot"
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-09"
---

# Phase 1 — Validation Strategy

> Planning contract only. No test files are added or executed in this task.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | None selected; automated tests are outside this task's scope. |
| **Config file** | None. |
| **Quick run command** | `dotnet build AgroPlan.sln` after the solution exists. Build only; do not run test commands. |
| **Full suite command** | Not applicable. |
| **Estimated runtime** | Unknown until the solution exists. |

---

## Sampling Rate

- No automated test sampling is planned for this task.
- A successful build is a compile check, not behavioral verification.
- Do not claim behavioral acceptance without an explicitly authorized verification pass.

---

## Per-Task Verification Map

| Requirement | Secure behavior to preserve | Planned evidence |
|-------------|-----------------------------|-----------------|
| REQ-pilot-accounts | Identity owns credentials; recovery uses a one-time code and support review; support never sees passwords. | Build plus implementation review. |
| REQ-pilot-farms-parcels, REQ-rf-g-01 | Every farm-scoped operation checks current active membership; cross-farm resource links are rejected. | Build plus authorization-path review. |
| REQ-pilot-crop-cycle, REQ-rf-g-38 | Catalog contains five crops and stores parameter source/validation state; missing parameters remain missing. | Build plus catalog/rule review. |
| REQ-pilot-sowing-plan, REQ-rf-g-10 | Gemini and climate produce a reviewable draft with source, freshness, uncertainty, and missing inputs; no unsupported numeric advice. | Build plus AI/climate boundary review. |
| REQ-pilot-planting-frame, REQ-rf-g-11 | Capacity calculations use only validated deterministic rules and usable area; the user chooses before confirmation. | Build plus rule/approval-path review. |
| REQ-pilot-manual-logbook, REQ-rf-g-05 | Corrections append linked records; optional photos are authorized for the same farm and binary data stays in Storage. | Build plus logbook/upload-path review. |

---

## Wave 0 Requirements

- [ ] Establish the solution and runtime projects.
- [ ] Do not create a test project or test files in this task.
- [ ] Keep any future test framework decision separate until the user requests tests or implementation verification.

---

## Manual-Only Verifications

Behavioral verification is not authorized in this task. The plan may describe acceptance scenarios, but no test suite, test files, or manual verification run is included here.

---

## Validation Sign-Off

- [ ] All tasks have an authorized verification approach.
- [ ] Sampling continuity: not applicable; no tests are planned.
- [ ] Wave 0 covers current build prerequisites.
- [ ] No watch-mode flags.
- [ ] Feedback latency: not applicable.
- [ ] `nyquist_compliant: true` set in frontmatter.

**Approval:** pending
