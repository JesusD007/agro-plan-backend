---
schema_version: 1
open_count: 2
waived_count: 0
fixed_count: 0
total_count: 2
last_updated: 2026-11-10T03:34:15.140Z
---

# Broken Windows Ledger

> Cross-phase defect register. With `workflow.windows_enforce` enabled, `/gsd-ship` blocks while `open_count > 0`.
> Waive with `gsd-tools windows waive <id> "<reason>"` (reason required).
> Mark fixed with `gsd-tools windows fixed <id>`.

| id | phase | kind | file | line | description | status | reason | recorded_at | resolved_at |
|----|-------|------|------|------|-------------|--------|--------|-------------|-------------|
| 1 | 01 | stub | src/AgroPlan.Worker/Program.cs |  | Worker entry point is intentionally idle; proposal consumers are owned by later phase plans. | open |  | 2026-11-10T03:34:14.012Z |  |
| 2 | 01 | unrun-verify | src/AgroPlan.sln |  | Compile-only dotnet build was not run because the host has SDK 8.0.204 and the plan targets .NET 10; SDK 10 is a required prerequisite. | open |  | 2026-11-10T03:34:15.140Z |  |

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
    "description": "Compile-only dotnet build was not run because the host has SDK 8.0.204 and the plan targets .NET 10; SDK 10 is a required prerequisite.",
    "status": "open",
    "reason": "",
    "recorded_at": "2026-11-10T03:34:15.140Z",
    "resolved_at": null,
    "milestone": null
  }
]
````
