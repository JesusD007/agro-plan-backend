---
phase: 02-farm-operations-and-safety
plan: 03
subsystem: api
tags: [aspnetcore, weather, prices, xunit-v3, scalar]
requires:
  - phase: 01-farm-planning-pilot
    provides: Farm membership authorization and development-only Scalar/OpenAPI
provides:
  - Farm-scoped provider-neutral weather advisory contract with provenance, validity, and stale/unavailable states
  - Farm-scoped reference-price contract retaining crop, market, region, unit, currency, source, and validity
  - Unavailable-by-default providers until approved weather and market sources are configured
affects: [crop-calendars, reports, operations]
actuals:
  tokens: 5219
  tasks: 2
  commits: 2
plan_head_before: 0fd79087db7e498c0f5b8ca55a87affc5a6c64fc
plan_head_after: eb1024f68e8785d5804e0d45c07b5eeb64224f40
tech-stack:
  added: []
  patterns: [provider-neutral contracts, provenance-validated results, unavailable-by-default adapter]
key-files:
  created:
    - src/AgroPlan.Application/Abstractions/IWeatherAdvisoryProvider.cs
    - src/AgroPlan.Application/Abstractions/IReferencePriceProvider.cs
    - src/AgroPlan.Infrastructure/Climate/UnavailableWeatherAdvisoryProvider.cs
    - src/AgroPlan.Infrastructure/Prices/UnavailableReferencePriceProvider.cs
    - src/AgroPlan.Api/Controllers/WeatherAdvisoriesController.cs
    - src/AgroPlan.Api/Controllers/ReferencePricesController.cs
    - src/AgroPlan.UnitTests/Advisories/AdvisoryFreshnessTests.cs
    - src/AgroPlan.IntegrationTests/Api/AdvisoriesTests.cs
  modified:
    - src/AgroPlan.Api/Program.cs
key-decisions:
  - "No weather or price provider is activated until its source and freshness policy are approved."
  - "Missing weather is unavailable, never a no-risk conclusion; missing prices have no numeric amount."
patterns-established:
  - "Available advisory data must carry provider/source, geography/market, observation or issue time, and validity."
  - "Farm-scoped advisory endpoints use the current per-request membership authorization policy."
requirements-completed: [REQ-rf-g-08, REQ-rf-g-35]
coverage:
  - id: D1
    description: Weather advisories expose provider provenance, geography, issue/validity, and safe unavailable/stale status.
    requirement: REQ-rf-g-08
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Advisories/AdvisoryFreshnessTests.cs
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/AdvisoriesTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Reference prices preserve market channel, geography, unit, currency, source, and time validity.
    requirement: REQ-rf-g-35
    verification:
      - kind: unit
        ref: src/AgroPlan.UnitTests/Advisories/AdvisoryFreshnessTests.cs
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/AdvisoriesTests.cs
        status: pass
    human_judgment: false
duration: 15min
completed: 2026-11-10
status: complete
---

# Phase 2 Plan 03: Safe Weather and Reference Price Contracts Summary

**Farm-scoped weather and reference-price APIs return source-aware freshness metadata and explicit unavailable states until providers are approved.**

## Performance

- **Duration:** 15 min
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments

- Added neutral weather and market-price provider interfaces and unavailable defaults.
- Added freshness/provenance evaluation; expired or geographically mismatched advisories cannot appear current.
- Exposed farm-authorized endpoints and documented routes through generated OpenAPI/Scalar in Development only.
- Added xUnit v3 unit and PostgreSQL-backed API coverage for unavailable results, provenance, membership, and docs exposure.

## Task Commits

1. **Task 1: Expose provider-neutral weather advisories with freshness state** - `60ca053` (feat)
2. **Task 2: Expose attributed reference-price queries without choosing a feed** - `eb1024f` (feat)

## Decisions Made

- Weather and price providers remain unavailable until source/freshness policy approval; no forecast or price is synthesized.
- Weather results require matching geography and active validity; price observations require source, crop/channel/region, unit/currency and valid-until.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Kept unavailable/default enum results explicit in the JSON contract.**
- **Found during:** Task 1 API integration test
- **Issue:** Default numeric enum JSON was not a readable API status.
- **Fix:** Applied string enum converters for both provider result statuses and asserted their serialized form.
- **Files modified:** `src/AgroPlan.Application/Abstractions/IWeatherAdvisoryProvider.cs`, `src/AgroPlan.Application/Abstractions/IReferencePriceProvider.cs`
- **Verification:** API integration tests passed.
- **Committed in:** `60ca053`, `eb1024f`

**Total deviations:** 1 auto-fixed (Rule 1)
**Impact on plan:** Makes status values explicit; no scope change.

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --no-restore -m:1 --filter FullyQualifiedName~AdvisoryFreshnessTests` — 4 passed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --no-restore -m:1 --filter FullyQualifiedName~AdvisoriesTests` — 2 passed.
- `dotnet build src/AgroPlan.sln --no-restore -m:1` — passed, 0 errors. NuGet vulnerability metadata emitted NU1900 warnings because nuget.org was unreachable.

## Issues Encountered

- The first testhost launch inside the sandbox failed to connect. Re-running outside the sandbox succeeded; one integration assertion then exposed enum serialization behavior, which was corrected and verified.

## User Setup Required

None for safe unavailable operation. A source and freshness policy must be selected before any weather or price provider is enabled in production.

## Next Phase Readiness

Weather and prices can now be consumed through typed, provider-neutral APIs while remaining unavailable by default. Continue with the harvest-safety plan and calendar/report plans.

## Self-Check: PASSED

- All plan-created source and test files exist.
- Both task commits are ancestors of current `develop` HEAD.
- `plan_head_before` and `plan_head_after` match the measured commit range.

---
*Phase: 02-farm-operations-and-safety*
*Completed: 2026-11-10*
