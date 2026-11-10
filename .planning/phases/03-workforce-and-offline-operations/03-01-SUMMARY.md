---
phase: 03-workforce-and-offline-operations
plan: 01
subsystem: workforce, api, database
tags: [aspnet-core, xunit-v3, postgres, testcontainers, invitations, authorization]

requires:
  - phase: 01-foundation
    provides: Identity users, verified email accounts, farm memberships, and email sender
provides:
  - Seven-day, one-time worker invitations with SHA-256 digests persisted in PostgreSQL
  - Verified email acceptance that atomically activates farm-scoped Worker membership
  - Farm-manager issue, renewal, and cancellation APIs with serialized worker seat limits
  - Server-owned Basic/Premium worker entitlement, defaulting to Basic
affects: [03-02-worker-tasks, workforce, authorization, farm-entitlements]

actuals:
  tokens: 9823
  tasks: 2
  commits: 2
  plan_head_before: 5c2244712e62960a30d31367233dab8434d87fc1
  plan_head_after: 48b464fa54b3d92a46db2cc20bb41c93bdc0f1b4

tech-stack:
  added: []
  patterns:
    - Database farm-row lock serializes pending invitations and five-worker seat reservation
    - Invitation token is generated with 256 bits of entropy; only its SHA-256 digest is stored
    - Worker activation and invitation consumption commit in one PostgreSQL transaction

key-files:
  created:
    - src/AgroPlan.Domain/Workforce/WorkerInvitation.cs
    - src/AgroPlan.Api/Controllers/WorkerInvitationsController.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/WorkerInvitationConfiguration.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/WorkerInvitations.cs
    - src/AgroPlan.UnitTests/Workforce/WorkerInvitationTests.cs
    - src/AgroPlan.IntegrationTests/Api/WorkerInvitationIntegrationTests.cs
  modified:
    - src/AgroPlan.Domain/Farms/Farm.cs
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
    - src/AgroPlan.Infrastructure/Persistence/Configurations/FarmConfiguration.cs
    - src/AgroPlan.IntegrationTests/IntegrationSupport.cs
    - src/AgroPlan.Api/appsettings.Development.example.json

key-decisions:
  - "Invitation email is normalized for matching; a verified account must match it exactly after normalization."
  - "Basic farms reserve five seats across active Workers and pending invitations; Premium entitlement is provisioned by trusted server-side code."
  - "The client acceptance URL must be configured as HTTPS before issue or renewal; the API fails closed when absent."

patterns-established:
  - "Use PostgreSQL row locks for farm-scoped seat reservation; no process-local locking for distributed API instances."
  - "Never persist, log, or return the raw invitation token; send it only through the existing email sender."

requirements-completed: [REQ-rf-g-06, REQ-rf-g-14, REQ-rf-g-17]
coverage:
  - id: D1
    description: Farm managers can issue, renew, and cancel seven-day worker invitations.
    requirement: REQ-rf-g-06
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerInvitationIntegrationTests.cs#Administrator_can_renew_and_cancel_pending_invitation
        status: pass
    human_judgment: false
  - id: D2
    description: Only a verified account with matching email can accept once and become a farm Worker.
    requirement: REQ-rf-g-14
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerInvitationIntegrationTests.cs#Owner_can_invite_verified_email_and_only_digest_is_persisted
        status: pass
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerInvitationIntegrationTests.cs#Unverified_or_mismatched_account_cannot_accept_invitation
        status: pass
    human_judgment: false
  - id: D3
    description: Basic and premium worker entitlements are enforced from server-owned farm state.
    requirement: REQ-rf-g-17
    verification:
      - kind: integration
        ref: src/AgroPlan.IntegrationTests/Api/WorkerInvitationIntegrationTests.cs#Basic_tier_reserves_five_seats_atomically_and_premium_has_no_worker_cap
        status: pass
      - kind: unit
        ref: src/AgroPlan.UnitTests/Workforce/WorkerInvitationTests.cs#Invitation_expires_after_seven_days_and_accepts_matching_verified_email_once
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-10
status: complete
---

# Phase 3 Plan 1: Worker Invitations Summary

**PostgreSQL-backed worker invitations use expiring digest-only tokens, verified email matching, atomic membership activation, and farm-locked seat enforcement.**

## Performance

- **Duration:** 25 minutes
- **Started:** 2026-11-10T20:22:00Z
- **Completed:** 2026-11-10T20:47:00Z
- **Tasks:** 2/2
- **Files modified:** 11

## Accomplishments

- Added the Worker membership role and a persisted invitation lifecycle with seven-day expiry, one-time acceptance, renewal, cancellation, and replay protection.
- Added farm-scoped API operations; verified matching accounts receive Worker membership in the same transaction that consumes the invitation.
- Enforced five active or pending worker seats for Basic farms and no worker cap for server-provisioned Premium farms, serialized by PostgreSQL farm-row locking.
- Confirmed email links are only issued when an HTTPS client acceptance URL is configured; the token itself is never persisted or logged.

## Task Commits

1. **Task 1: Worker invitation acceptance end-to-end tracer** — `c6a7bb3` (test RED)
2. **Task 1 implementation and Task 2 lifecycle, entitlement, and capacity behavior** — `48b464f` (feat GREEN)

## Verification

- `dotnet test src/AgroPlan.UnitTests/AgroPlan.UnitTests.csproj --filter "FullyQualifiedName~WorkerInvitationTests"` — 4 passed, 0 failed.
- `dotnet test src/AgroPlan.IntegrationTests/AgroPlan.IntegrationTests.csproj --filter "FullyQualifiedName~WorkerInvitationIntegrationTests"` — 4 passed, 0 failed, using PostgreSQL Testcontainers on Docker Desktop.
- TDD RED evidence: the xUnit v3 role and endpoint tests executed and failed on the expected assertions (missing Worker role and HTTP 404). The GSD evidence checker accepted the test results after faithfully adapting VSTest TRX test names/outcomes into its supported TAP format. GREEN passed the same targeted tests.

## Decisions Made

- Pending invitations count toward Basic capacity along with active Worker memberships; Owner and Administrator memberships do not consume worker seats.
- The Premium entitlement is not accepted from public request data and remains writable only through trusted platform provisioning.
- A client HTTPS acceptance URL must be configured before invitation issue or renewal to avoid sending unusable links.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical configuration] Fail closed when no HTTPS client acceptance URL is configured.**
- **Found during:** Task 1 (invitation email delivery)
- **Issue:** A made-up default URL would make delivered invitations unusable and could expose tokens to an incorrect destination.
- **Fix:** Require `WorkerInvitations:AcceptanceUrl`, validate it as an absolute HTTPS URL without query, fragment, or user-info, and return 503 before creating the invitation when it is absent. Added the setting to the development example configuration and test host.
- **Verification:** PostgreSQL-backed issue/accept test confirms the configured URL sends a token that is not stored in plaintext.
- **Committed in:** `48b464f`

**2. [Rule 2 - Concurrency correctness] Lock invitation rows during renewal and cancellation.**
- **Found during:** Task 2 (lifecycle operations)
- **Issue:** Concurrent acceptance, renewal, or cancellation could otherwise update stale lifecycle state.
- **Fix:** Serialize renewal and cancellation against acceptance with PostgreSQL row locks and transactions.
- **Verification:** Renewal/cancellation integration test and full targeted integration suite pass.
- **Committed in:** `48b464f`

---

**Total deviations:** 2 auto-fixed (Rule 2)
**Impact on plan:** Both changes preserve the planned behavior while preventing unusable email links and lifecycle races.

## Issues Encountered

- The system `dotnet` still resolves to SDK 8; tests used `C:/Users/romer/.dotnet/dotnet.exe` 10.0.404 and the repo-local `.dotnet-home` for CLI state.
- Docker's named pipe requires elevated process access in this workspace; PostgreSQL Testcontainers tests passed when executed with approved elevated access.
- The GSD RED evidence parser does not accept VSTest TRX directly, so the actual TRX test identities and outcomes were faithfully adapted to TAP for the classifier.

## User Setup Required

Set `WorkerInvitations:AcceptanceUrl` (environment variable `WorkerInvitations__AcceptanceUrl`) to the client’s HTTPS acceptance page before enabling invitations. The app’s exact client URL remains deployment-specific.

## Next Phase Readiness

Phase 3 Plan 03-02 can add task assignment on top of the persisted Worker membership and invitation acceptance flow.

## TDD Gate Compliance

- **RED:** `Worker_role_is_part_of_the_farm_membership_contract` failed because `FarmRole.Worker` did not exist. `Owner_can_invite_verified_email_and_only_digest_is_persisted` failed because the route returned 404. Both failures were test assertions, not build or fixture failures.
- **GREEN:** The domain, persistence, API, and entitlement implementation made the four unit tests and four PostgreSQL integration tests pass.
- **REFACTOR:** Added HTTPS URL validation and transaction locks, then reran the targeted tests successfully.

## Self-Check: PASSED

- All planned domain, API, persistence, migration, and test artifacts exist.
- Commits `c6a7bb3` and `48b464f` are ancestors of HEAD.
- Summary measurement ledger: base `5c2244712e62960a30d31367233dab8434d87fc1`, plan HEAD `48b464fa54b3d92a46db2cc20bb41c93bdc0f1b4`, 2 commits before metadata close-out.
