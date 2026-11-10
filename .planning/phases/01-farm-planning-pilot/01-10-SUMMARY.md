---
phase: 01-farm-planning-pilot
plan: 10
status: complete
completed: 2026-11-10
---

# Plan 01-10 Summary: xUnit v3 Unit Coverage

## Outcome

Added `AgroPlan.UnitTests` to the solution with xUnit v3, VSTest discovery, and isolated unit coverage for crop catalog behavior, deterministic planting-frame calculations, sowing proposal confirmation guards, and immutable logbook corrections.

The first runner configuration used Microsoft Testing Platform, which did not work reliably with the repository's `dotnet test` invocation. It was changed to xUnit v3 3.2.2 with `xunit.runner.visualstudio` 4.0.1 and `Microsoft.NET.Test.Sdk` 18.11.0. The supported command is:

```powershell
dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1
```

## Verification

- **Result:** 14 passed, 0 failed, 0 skipped.
- **External services:** not required for this suite.
- **Agronomic fixtures:** synthetic values exist only in tests; runtime catalog seeds remain unchanged.

## Scope Adjustment

Recovery and asynchronous worker behavior depend on PostgreSQL-backed operations (`FromSqlInterpolated` and conditional `ExecuteUpdateAsync`). Those cases are assigned to plan 01-11's isolated integration suite rather than asserting behavior through weak in-memory substitutes. Unit tests cover the deterministic rules and confirmation invariants directly.

## Files

- `src/AgroPlan.sln`
- `src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj`
- `src/AgroPlan.UnitTests/xunit.runner.json`
- Crop catalog, planting-frame, proposal workflow, and logbook correction tests.
