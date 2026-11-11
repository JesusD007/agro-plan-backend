# Phase 4: Applied Farm Intelligence - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-11
**Phase:** 4-Applied Farm Intelligence
**Areas discussed:** Voice activity drafts, weekly summaries, photo symptom reports

---

## Voice activity drafts

| Option | Description | Selected |
|--------|-------------|----------|
| Español dominicano | Support Dominican Spanish | |
| Español y creole haitiano | Support both languages | ✓ |
| Otro conjunto de idiomas | User specifies a different language set | |

**User's choice:** Español dominicano y creole haitiano.
**Notes:** The user confirmed partial editable drafts with uncertain information marked, worker voice drafts limited to assigned tasks, and maximum recording length of five minutes.

| Option | Description | Selected |
|--------|-------------|----------|
| Draft partial and mark unclear fields | Let the user correct uncertain transcription | ✓ |
| Request a new recording | Do not create a draft when audio is unclear | |
| Vary by confidence | Use a confidence threshold to choose behavior | |

**User's choice:** Create a partial draft and mark uncertain parts for correction.

| Option | Description | Selected |
|--------|-------------|----------|
| Assigned tasks only | Worker can draft records for their assigned tasks | ✓ |
| Any farm activity for manager review | Broader worker recording scope | |
| Task evidence only | Limit voice input to task evidence | |

**User's choice:** Only tasks assigned to the worker.

| Option | Description | Selected |
|--------|-------------|----------|
| One minute | Short recording limit | |
| Three minutes | Moderate recording limit | |
| Five minutes | Longer field note limit | ✓ |

**User's choice:** Up to five minutes.

---

## Weekly summaries

| Option | Description | Selected |
|--------|-------------|----------|
| Monday–Sunday in farm timezone | Calendar week local to the farm | ✓ |
| Rolling seven days | Trailing seven-day interval | |
| User-selected dates | Custom reporting period | |

**User's choice:** Calendar week in the farm time zone.

| Option | Description | Selected |
|--------|-------------|----------|
| On request | Manager requests a summary when needed | ✓ |
| Scheduled weekly | Generate automatically at week close | |
| Both | On-demand and scheduled generation | |

**User's choice:** On request by the manager.

| Option | Description | Selected |
|--------|-------------|----------|
| Whole farm and filter by crop cycle | Farm summary with cycle filter | ✓ |
| Whole farm only | No crop-cycle filter | |
| One crop cycle per summary | Generate a summary for a single cycle | |

**User's choice:** Whole farm with option to filter by crop cycle.

| Option | Description | Selected |
|--------|-------------|----------|
| Owner and farm administrator | Share with active, compatible advisor grant | ✓ |
| Owner only | Only farm owner can share | |
| Any editor | Any farm role with edit access can share | |

**User's choice:** Owner and farm administrator may review/share with an active scope-compatible grant.

| Option | Description | Selected |
|--------|-------------|----------|
| Summary text only | Advisor sees no source records | |
| Summary plus allowed source records | Source access remains bounded by the active grant | ✓ |
| All farm detail | Give advisor broad farm access | |

**User's choice:** Advisor may view summary and listed source records only when allowed by the current grant.

---

## Photo symptom reports

| Option | Description | Selected |
|--------|-------------|----------|
| Describe visible signs and request missing photos | No AI diagnosis or treatment before agronomist review | ✓ |
| Offer unapproved hypotheses | Include tentative possibilities | |
| Produce provisional diagnosis and treatment | AI provides a provisional diagnosis/treatment | |

**User's choice:** Describe visible signs only and request additional photos; no diagnosis or treatment.

| Option | Description | Selected |
|--------|-------------|----------|
| Verified agronomist assigned/authorized for farm | Explicitly authorized reviewer confirms | ✓ |
| Any active advisor | Any advisor grant holder can confirm | |
| Any farm administrator | Administrator can confirm agronomic assessment | |

**User's choice:** A verified agronomist explicitly assigned or authorized for the farm.

| Option | Description | Selected |
|--------|-------------|----------|
| Request more evidence and remain pending | Keep open until sufficient photos/data arrive | ✓ |
| Close as inconclusive | End review without more evidence | |
| Confirm with warning | Accept diagnosis despite weak evidence | |

**User's choice:** Request more photos/data and keep the report pending.

| Option | Description | Selected |
|--------|-------------|----------|
| Delete photos on close/rejection and retain report | Keep the report while deleting source images | ✓ |
| Keep images until producer deletes them | Retain source photos with the report | |
| Farm-configurable automatic deletion | Allow per-farm retention settings | |

**User's choice:** Delete photos when review closes or is rejected, retaining the report.

---

## the agent's Discretion

- Technical language detection, transcription, structured output validation, cleanup job mechanics, privacy-minimized provider payloads, and AI evaluation methods.

## Provider compatibility follow-up

Google's current Gemini transcription language documentation did not confirm Haitian Creole support. The user selected a provider-neutral speech contract, with provider validation and selection required before activation. Do not silently ship Spanish-only support; the voice capability must meet both language requirements or remain unavailable until a compatible provider is selected.

## Deferred Ideas

None.
