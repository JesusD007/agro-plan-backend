---
schema_version: 1
open_count: 2
waived_count: 0
fixed_count: 1
total_count: 3
last_updated: 2026-11-10T04:47:15.572Z
---

# Broken Windows Ledger

> Cross-phase defect register. With `workflow.windows_enforce` enabled, `/gsd-ship` blocks while `open_count > 0`.
> Waive with `gsd-tools windows waive <id> "<reason>"` (reason required).
> Mark fixed with `gsd-tools windows fixed <id>`.

| id | phase | kind | file | line | description | status | reason | recorded_at | resolved_at |
|----|-------|------|------|------|-------------|--------|--------|-------------|-------------|
| 1 | 01 | stub | src/AgroPlan.Worker/Program.cs |  | Worker entry point is intentionally idle; proposal consumers are owned by later phase plans. | open |  | 2026-11-10T03:34:14.012Z |  |
| 2 | 01 | unrun-verify | src/AgroPlan.sln |  | Initial compile-only check was deferred while .NET SDK 10 was missing; completed successfully with SDK 10.0.403 after local setup. | fixed |  | 2026-11-10T03:34:15.140Z | 2026-11-10T03:43:26.453Z |
| 3 | 01 | stub | src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs |  | Proposal generation returns explicit provider-not-configured failure until Plan 01-06 supplies Gemini/provider processing; no draft is fabricated. | open |  | 2026-11-10T04:47:15.572Z |  |

````json
[
  {
    "id": 1,
    "kind": "stub",
    "phase": "01",
    "file": "src/AgroPlan.Worker/Program.cs",
    "line": null,
    "description": "Worker entry point is intentionally idle; proposal consumers are owned by later phase plans.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-10T03:34:14.012Z",
    "resolved_at": null,
    "milestone": null
  },
  {
    "id": 2,
    "kind": "unrun-verify",
    "phase": "01",
    "file": "src/AgroPlan.sln",
    "line": null,
    "description": "Initial compile-only check was deferred while .NET SDK 10 was missing; completed successfully with SDK 10.0.403 after local setup.",
    "status": "fixed",
    "reason": "",
    "recorded_at": "2026-11-10T03:34:15.140Z",
    "resolved_at": "2026-11-10T03:43:26.453Z",
    "milestone": null
  },
  {
    "id": 3,
    "kind": "stub",
    "phase": "01",
    "file": "src/AgroPlan.Worker/Jobs/SowingPlanConsumer.cs",
    "line": null,
    "description": "Proposal generation returns explicit provider-not-configured failure until Plan 01-06 supplies Gemini/provider processing; no draft is fabricated.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-10T04:47:15.572Z",
    "resolved_at": null,
    "milestone": null
  }
]
````
