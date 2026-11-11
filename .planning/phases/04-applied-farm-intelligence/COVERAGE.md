# Phase 04 API Coverage

## External provider declaration

Phase 04 does not call a new external AI provider. Speech, weekly-summary prose, and symptom-observation ports remain application-owned and provider-neutral. Deterministic fake and unconfigured adapters are limited to development/test use. In production, voice upload is unavailable until an approved compatible speech provider passes both language gates; summary prose remains unavailable without an approved narrative provider while the deterministic projection remains available; symptom reports can be submitted for manual agronomist review while automated observation remains unavailable. No operation dispatches data to an unselected provider, and a fake adapter is never registered for production use.

The existing Gemini adapter remains scoped to the earlier proposal workflows. Its presence does not mean that Phase 04 speech, summary, or image operations use Gemini. In particular, the available documentation did not verify Haitian Creole transcription support; the Spanish and Haitian Creole requirement cannot be reduced to Spanish-only behavior.

No new SDK, AI framework, external HTTP endpoint, or model account is required by these plans. If a provider integration is selected later, add its request, response, data-retention, key-management, capability, cost, and failure-mode coverage here before wiring it into production.

## AgroPlan API and Scalar coverage

The phase adds farm-scoped API routes and keeps the existing development-only OpenAPI/Scalar surface. Each feature plan adds an integration test that inspects `/openapi/v1.json`, confirms the feature routes and auth contracts are present, and confirms `/scalar/` resolves in Development. Production continues to hide both documentation routes.

| Feature | Planned route surface | Contract coverage |
|---|---|---|
| Voice drafts | `POST /api/farms/{farmId}/voice-drafts/upload-authorizations`, `POST /api/farms/{farmId}/voice-drafts`, `GET /api/farms/{farmId}/voice-drafts/{draftId}`, `POST /api/farms/{farmId}/voice-drafts/{draftId}/confirm` | `VoiceDraftOpenApiTests` and PostgreSQL/Worker integration tests |
| Weekly summaries | `POST /api/farms/{farmId}/weekly-summaries`, `GET /api/farms/{farmId}/weekly-summaries/{summaryId}`, `POST /api/farms/{farmId}/weekly-summaries/{summaryId}/share`, `GET /api/farms/{farmId}/advisor-weekly-summaries/{summaryId}`, and `GET /api/farms/{farmId}/advisor-weekly-summaries/{summaryId}/sources/{sourceId}` | `WeeklySummaryOpenApiTests` and PostgreSQL authorization/projection tests |
| Symptom reports | `POST /api/farms/{farmId}/symptom-reports/upload-authorizations`, `POST /api/farms/{farmId}/symptom-reports`, `GET /api/farms/{farmId}/symptom-reports/{reportId}`, `GET /api/farms/{farmId}/symptom-reports/{reportId}/photos/{photoId}`, `POST`/`DELETE /api/farms/{farmId}/agronomist-authorizations`, and `POST /api/farms/{farmId}/symptom-reports/{reportId}/review` | `SymptomReportOpenApiTests` and PostgreSQL/storage/Worker integration tests |

## Production activation evidence still required

These plans keep provider-backed operations unavailable until all applicable gates are satisfied:

- Select a compatible provider separately for speech, summary prose, and image observation; verify its language and schema support before activation.
- For speech, validate Dominican Spanish, Haitian Creole, and code-switched field recordings separately with human reference transcripts. Do not enable either language based on a marketing locale list alone, and do not silently substitute Spanish-only coverage.
- Create the AI-SPEC reference set as 45 fictional metadata-only cases (25 voice: 10 Dominican Spanish, 10 Haitian Creole, 5 Spanish–Kreyòl code-switch; 10 summary; 10 symptom) with offline `Category=AiEval` tests. Keep real consented audio/photos private and outside git; the separate production speech set must include at least 20 consented representative recordings in each supported language plus code-switch cases and meet each language's field-accuracy threshold.
- Confirm paid-service terms, retention/deletion behavior, operational secrets, and privacy review before any real farm media is sent to a provider.
- Complete domain expert review for the summary prose and agronomist review of the symptom-observation evaluation set.

The API behavior without a selected production provider is an explicit unavailable/pending state. Deterministic source projections, authorization, user confirmation, agronomist authority, and media lifecycle continue to be tested without network calls.
