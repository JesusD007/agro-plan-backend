# Phase 04 API Coverage

## External provider declaration

Phase 04 does not call a new external AI provider. Speech, weekly-summary prose, and symptom-observation ports remain application-owned and provider-neutral. Deterministic fake and unconfigured adapters are limited to development/test use. Each uploaded voice recording and each symptom photo records its own explicit external-processing consent; missing or revoked consent preserves manual handling and blocks that object's provider call. In Production, voice remains off until an approved compatible speech provider passes both language gates; summary prose remains unavailable without an approved narrative provider while deterministic projection remains available; symptom reports can be submitted for manual agronomist review while automated observation remains unavailable. No operation dispatches data to an unselected provider, and a fake adapter is never registered for Production.

The existing Gemini adapter remains scoped to the earlier proposal workflows. Its presence does not mean that Phase 04 speech, summary, or image operations use Gemini. In particular, the available documentation did not verify Haitian Creole transcription support; the Spanish and Haitian Creole requirement cannot be reduced to Spanish-only behavior.

No new SDK, AI framework, external HTTP endpoint, or model account is required by these plans. If a provider integration is selected later, add its request, response, data-retention, key-management, capability, cost, and failure-mode coverage here before wiring it into production.

## AgroPlan API and Scalar coverage

The phase adds farm-scoped API routes and keeps the existing development-only OpenAPI/Scalar surface. Each feature plan adds an integration test that inspects `/openapi/v1.json`, confirms the feature routes and auth contracts are present, and confirms `/scalar/` resolves in Development. Production continues to hide both documentation routes.

| Feature | Planned route surface | Contract coverage |
|---|---|---|
| Voice drafts | `POST /api/farms/{farmId}/voice-drafts/upload-authorizations`, `POST /api/farms/{farmId}/voice-drafts`, `GET /api/farms/{farmId}/voice-drafts/{draftId}`, `POST /api/farms/{farmId}/voice-drafts/{draftId}/confirm` | `VoiceDraftOpenApiTests` class in `VoiceDraftIntegrationTests.cs`, consent/authorization/retention tests |
| Weekly summaries | `POST /api/farms/{farmId}/weekly-summaries`, `GET /api/farms/{farmId}/weekly-summaries/{summaryId}`, `POST /api/farms/{farmId}/weekly-summaries/{summaryId}/share`, `GET /api/farms/{farmId}/advisor-weekly-summaries/{summaryId}`, and `GET /api/farms/{farmId}/advisor-weekly-summaries/{summaryId}/sources/{sourceId}` | OpenAPI, Worker, and grant tests in `WeeklySummaryTests.cs` |
| Symptom reports | `POST /api/farms/{farmId}/symptom-reports/upload-authorizations`, `POST /api/farms/{farmId}/symptom-reports`, `GET /api/farms/{farmId}/symptom-reports/{reportId}`, `GET /api/farms/{farmId}/symptom-reports/{reportId}/photos/{photoId}`, `POST`/`DELETE /api/farms/{farmId}/agronomist-authorizations`, and `POST /api/farms/{farmId}/symptom-reports/{reportId}/review` | `SymptomReportOpenApiTests` class in `SymptomReportIntegrationTests.cs` plus PostgreSQL/storage/Worker tests |

## Production activation evidence still required

These plans keep provider-backed operations unavailable until all applicable gates are satisfied:

- Select a compatible provider separately for speech, summary prose, and image observation; verify its language and schema support before activation.
- For speech, verify the selected provider supports the exact transcription operation in Haitian Creole. Keep both speech languages disabled until the provider is selected/validated and achieves at least 95% exact field accuracy separately for Dominican Spanish and Haitian Creole against human-verified references; marketing locale claims are insufficient and Spanish-only activation is forbidden.
- Maintain the AI-SPEC 45-case reference composition: 25 voice (10 Dominican Spanish, 10 Haitian Creole, 5 Spanish–Kreyòl code-switch), 10 summary and 10 symptom cases. Offline `Category=AiEval` uses fictional metadata/fakes; any real audio/photo in evaluation requires per-object consent and stays private/outside git. Production speech evidence additionally requires at least 20 consented representative recordings in each language plus consented code-switch cases, with human reference transcripts and per-language 95% accuracy.
- Enforce media and summary bounds: one recording <=300 seconds and 10 MiB; <=5 JPEG/PNG/WebP photos per report, <=10 MiB each, <=8,192 pixels per edge, <=20 megapixels per image and <=50 megapixels total; <=500 combined weekly source/task rows (fail without truncation), <=64 KiB serialized model input and <=16 KiB model output. Use built-in fixed-window policies of 10 media-intake requests and 20 summary requests per user/farm/hour.
- Confirm paid-service terms, retention/deletion behavior, operational secrets, and privacy review before any real farm media is sent to a provider.
- Complete domain expert review for the summary prose and agronomist review of the symptom-observation evaluation set.

The API behavior without a selected production provider is an explicit unavailable/pending state. Deterministic source projections, authorization, user confirmation, agronomist authority, and media lifecycle continue to be tested without network calls.
