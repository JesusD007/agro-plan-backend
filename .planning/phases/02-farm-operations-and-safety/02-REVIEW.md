---
phase: 02-farm-operations-and-safety
reviewed: 2026-11-10T17:45:00Z
depth: standard
files_reviewed: 77
files_reviewed_list:
  - src/AgroPlan.Api/Authorization/AdvisorReportAuthorizationHandler.cs
  - src/AgroPlan.Api/Authorization/AdvisorReportRequirement.cs
  - src/AgroPlan.Api/Controllers/AdvisorAccessController.cs
  - src/AgroPlan.Api/Controllers/AdvisorReportsController.cs
  - src/AgroPlan.Api/Controllers/CropCalendarsController.cs
  - src/AgroPlan.Api/Controllers/ExpenseEntriesController.cs
  - src/AgroPlan.Api/Controllers/HarvestRecordsController.cs
  - src/AgroPlan.Api/Controllers/InputApplicationsController.cs
  - src/AgroPlan.Api/Controllers/IrrigationEntriesController.cs
  - src/AgroPlan.Api/Controllers/OperationsReportsController.cs
  - src/AgroPlan.Api/Controllers/ReferencePricesController.cs
  - src/AgroPlan.Api/Controllers/ScheduleChangeProposalsController.cs
  - src/AgroPlan.Api/Controllers/WeatherAdvisoriesController.cs
  - src/AgroPlan.Api/Program.cs
  - src/AgroPlan.Api/Reports/CsvReportWriter.cs
  - src/AgroPlan.Application/Abstractions/IAiProposalProvider.cs
  - src/AgroPlan.Application/Abstractions/IReferencePriceProvider.cs
  - src/AgroPlan.Application/Abstractions/IWeatherAdvisoryProvider.cs
  - src/AgroPlan.Application/AdvisoryAccess/AdvisorGrantService.cs
  - src/AgroPlan.Application/Calendars/CalendarExplanationDraftValidator.cs
  - src/AgroPlan.Application/Calendars/CropCalendarEvaluator.cs
  - src/AgroPlan.Application/Reports/CostReport.cs
  - src/AgroPlan.Application/Reports/OperationsReportReader.cs
  - src/AgroPlan.Application/Reports/ProductionReport.cs
  - src/AgroPlan.Application/Safety/HarvestEligibilityEvaluator.cs
  - src/AgroPlan.Application/UseCases/AppendExpenseEntry.cs
  - src/AgroPlan.Application/UseCases/CorrectExpenseEntry.cs
  - src/AgroPlan.Domain/AdvisoryAccess/AdvisorAccessGrant.cs
  - src/AgroPlan.Domain/Calendars/CropCalendarTask.cs
  - src/AgroPlan.Domain/Calendars/ScheduleChangeProposal.cs
  - src/AgroPlan.Domain/Operations/ExpenseEntry.cs
  - src/AgroPlan.Domain/Operations/HarvestEntry.cs
  - src/AgroPlan.Domain/Operations/InputApplicationEntry.cs
  - src/AgroPlan.Domain/Operations/IrrigationEntry.cs
  - src/AgroPlan.Domain/Safety/ValidatedProductLabel.cs
  - src/AgroPlan.Infrastructure/Climate/UnavailableWeatherAdvisoryProvider.cs
  - src/AgroPlan.Infrastructure/Messaging/Events/ScheduleChangeRequested.cs
  - src/AgroPlan.Infrastructure/Messaging/OutboxPublisher.cs
  - src/AgroPlan.Infrastructure/Messaging/RabbitMqPublisher.cs
  - src/AgroPlan.Infrastructure/Persistence/AgroPlanDbContext.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/AdvisorAccessGrantConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/CropCalendarConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/ExpenseEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/HarvestEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/InputApplicationEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/IrrigationEntryConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Configurations/ValidatedProductLabelConfiguration.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/AdvisorAccessGrants.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/CropCalendars.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/ExpenseEntries.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/HarvestSafety.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/InputApplications.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/IrrigationEntries.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/ScheduleChangeProposals.cs
  - src/AgroPlan.Infrastructure/Persistence/Migrations/ScheduleProposalProcessingLease.cs
  - src/AgroPlan.Infrastructure/Prices/UnavailableReferencePriceProvider.cs
  - src/AgroPlan.IntegrationTests/Api/AdvisorAccessTests.cs
  - src/AgroPlan.IntegrationTests/Api/AdvisoriesTests.cs
  - src/AgroPlan.IntegrationTests/Api/CropCalendarTests.cs
  - src/AgroPlan.IntegrationTests/Api/ExpenseEntriesTests.cs
  - src/AgroPlan.IntegrationTests/Api/HarvestSafetyTests.cs
  - src/AgroPlan.IntegrationTests/Api/InputApplicationsTests.cs
  - src/AgroPlan.IntegrationTests/Api/IrrigationEntriesTests.cs
  - src/AgroPlan.IntegrationTests/Api/OperationsReportsTests.cs
  - src/AgroPlan.IntegrationTests/Worker/OutboxAndConsumerTests.cs
  - src/AgroPlan.UnitTests/Advisories/AdvisoryFreshnessTests.cs
  - src/AgroPlan.UnitTests/AdvisoryAccess/AdvisorGrantTests.cs
  - src/AgroPlan.UnitTests/Calendars/CalendarExplanationValidationTests.cs
  - src/AgroPlan.UnitTests/Calendars/CropCalendarEvaluatorTests.cs
  - src/AgroPlan.UnitTests/Operations/ExpenseEntryTests.cs
  - src/AgroPlan.UnitTests/Operations/InputApplicationEntryTests.cs
  - src/AgroPlan.UnitTests/Operations/IrrigationEntryTests.cs
  - src/AgroPlan.UnitTests/Reports/OperationsReportTests.cs
  - src/AgroPlan.UnitTests/Safety/HarvestEligibilityEvaluatorTests.cs
  - src/AgroPlan.Worker/AI/GeminiProposalProvider.cs
  - src/AgroPlan.Worker/Jobs/ScheduleChangeProposalConsumer.cs
  - src/AgroPlan.Worker/Program.cs
findings:
  critical: 0
  warning: 0
  info: 0
  total: 0
  necessity: 0
status: clean
---

# Phase 2: Code Review Report

**Reviewed:** 2026-11-10
**Depth:** standard
**Files Reviewed:** 77
**Status:** clean

## Summary

Re-reviewed the Phase 2 source changes after all six code-review fixes. Production reports now select active sowing entries after linked corrections. The calendar consumes progress evidence once, CSV correction links are resolved before date filters, schedule proposals use active planting evidence, and expired Worker claims transition to a terminal state whether or not the proposal itself has expired. Focused regression tests are recorded in `02-REVIEW-FIX.md`.

All reviewed files meet quality standards. No issues found.

---

_Reviewer: gsd-code-reviewer_
_Depth: standard_
