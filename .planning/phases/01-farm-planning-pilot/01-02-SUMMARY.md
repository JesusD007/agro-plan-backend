---
phase: 01-farm-planning-pilot
plan: 02
subsystem: identity-and-recovery
tags: [identity, refresh-tokens, password-recovery, smtp, mailkit, support-audit]
dependency-graph:
  requires: [01-01]
  provides: [refresh-session-rotation, support-mediated-password-recovery, transactional-email-adapter]
  affects: [01-03, 01-04, 01-05, 01-06, 01-07, 01-08]
tech-stack:
  added: [MailKit 4.18.2]
  patterns: [hashed-opaque-refresh-tokens, token-family-replay-revocation, one-time-email-code, support-role-review, append-only-review-audit]
key-files:
  created:
    - src/AgroPlan.Api/Controllers/SupportRecoveryController.cs
    - src/AgroPlan.Application/Abstractions/IEmailSender.cs
    - src/AgroPlan.Infrastructure/Email/ConfiguredEmailSender.cs
    - src/AgroPlan.Infrastructure/Identity/RecoveryService.cs
    - src/AgroPlan.Infrastructure/Identity/RefreshSession.cs
    - src/AgroPlan.Infrastructure/Identity/RefreshSessionService.cs
    - src/AgroPlan.Infrastructure/Persistence/Migrations/AccountSessionsAndRecovery.cs
  modified:
    - src/AgroPlan.Api/Identity/IdentityEndpoints.cs
    - src/AgroPlan.Api/Identity/TokenIssuer.cs
    - src/AgroPlan.Api/Program.cs
    - src/AgroPlan.Api/appsettings.json
    - src/AgroPlan.Api/appsettings.Development.example.json
    - src/AgroPlan.Infrastructure/AgroPlan.Infrastructure.csproj
    - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
decisions:
  - Opaque 256-bit refresh tokens are stored as SHA-256 hashes, rotate with a fixed 30-day family expiry, and revoke the family on replay.
  - Recovery email, source IP, code, and password-change token are stored as keyed hashes derived from the server signing key.
  - Recovery requests use generic public responses; code verification requires an 8-digit code, 15-minute expiry and five attempts.
  - Support review requires the seeded Support Identity role, records an audit entry and sends a separate 15-minute one-time token; password reset revokes all refresh sessions.
  - SMTP transport is configurable and enforces STARTTLS or TLS-on-connect; provider credentials remain server-side settings.
metrics:
  duration: 18m
  completed_date: 2026-11-10
  commits: 1
  plan_head_before: d61d42e0256cb910da4f3fd4f7f3137694e74ebc
  plan_head_after: c8387558f62d4f7553ec571e450d60eadada73c1
actuals:
  tokens: 12521
  tasks: 2
  commits: 1
status: complete
---

# Phase 1 Plan 2: Account Sessions and Support-Mediated Recovery Summary

Added rotating refresh sessions and a support-reviewed recovery lifecycle. Public recovery responses do not confirm whether an account exists; secrets are stored as hashes and recovery approval never exposes or lets staff set a password.

## Progress

| Plan task | Status | Commit |
|---|---|---|
| Task 2: Owner/admin credential and refresh-session lifecycle | Complete; solution build passed | `c838755` |
| Task 3: Audited support-mediated recovery | Complete; solution build passed | `c838755` |

The compile-only command `dotnet build src/AgroPlan.sln --no-restore` passed with .NET SDK 10.0.403 and 0 warnings/errors. No tests were added or run; no database or email provider was provisioned.

## Security and Persistence Notes

- Access JWTs now include Identity role claims so only an authenticated account assigned the `Support` role can list or review requests. The schema migration seeds the role; no public endpoint can grant it.
- Refresh tokens contain 256 bits of randomness, are persisted only as SHA-256 hashes, rotate under a transaction, retain a fixed 30-day family expiry, and revoke the full family if a consumed token is replayed. Sign-out revokes one session; sign-out-all revokes every active session for the account.
- Recovery requests store keyed hashes of normalized email, source IP, email code and reset token. Account/IP request caps are 3 and 10 per hour; code lifetime is 15 minutes with at most five attempts. Code verification locks the request row to serialize attempts.
- Support sees only email-verified requests. Review decisions and notes are written to `identity.recovery_audit`; approval sends a distinct one-time 15-minute password-change token. Successful reset changes the password through ASP.NET Core Identity and revokes refresh sessions.
- Email delivery uses MailKit 4.18.2 with STARTTLS by default or TLS-on-connect when configured. `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUseSslOnConnect`, `Email:SmtpUserName`, `Email:SmtpPassword`, and `Email:FromAddress` are server-side settings. The provider is not provisioned in this plan.
- Migration `20261110000001_AccountSessionsAndRecovery` has not been applied to a database. A trusted operator must provision Support-role assignments before staff can review recovery requests.

## Deviations from Plan

- Reused `src/AgroPlan.Api/Identity/TokenIssuer.cs`, created by Plan 01-01, rather than relocating it to the Infrastructure path named in the plan. This avoids moving an already integrated component while supporting the same token contract.
- Added a migration for refresh-session and recovery state because the plan requires persistent rotation, rate limits, audit and one-time token status.
- Added MailKit 4.18.2 for SMTP transport after checking its official NuGet publisher, MIT license, release information and .NET 8+ framework compatibility; it compiled successfully on .NET 10. Microsoft documents `System.Net.Mail.SmtpClient` as not recommended for new development.

## Known Setup Requirements

- Configure the server-side SMTP settings before enabling recovery in production. The checked-in settings contain no provider password or real credentials.
- Assign the `Support` role only through trusted operator provisioning. Support-role issuance is absent from public registration and account APIs.

## Self-Check: PASSED

- Required controller, service, email adapter, model configurations and migration exist.
- Commit `c838755` is present and contains the implementation.
- Compile-only solution build passed with no warnings or errors.
- No tests or manual acceptance scenarios were run.
