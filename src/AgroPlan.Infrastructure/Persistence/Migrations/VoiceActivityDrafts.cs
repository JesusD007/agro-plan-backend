using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261111000025_VoiceActivityDrafts")]
public sealed class VoiceActivityDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.voice_activity_drafts (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"WorkTaskId\" uuid, \"ParcelId\" uuid NOT NULL, \"CropCycleId\" uuid, " +
        "\"SubmittedByUserId\" varchar(450) NOT NULL, \"ConsentActorUserId\" varchar(450) NOT NULL, \"ConsentNoticeVersion\" varchar(80) NOT NULL, " +
        "\"ConsentedToExternalProcessing\" boolean NOT NULL, \"ConsentedAt\" timestamptz NOT NULL, \"ConsentRevokedAt\" timestamptz, " +
        "\"Language\" varchar(32) NOT NULL CHECK (\"Language\" IN ('DominicanSpanish','HaitianCreole')), " +
        "\"UploadObjectKey\" varchar(1024) NOT NULL, \"AudioObjectKey\" varchar(1024), \"ContentType\" varchar(64) NOT NULL CHECK (\"ContentType\" = 'audio/wav'), " +
        "\"ExpectedSizeBytes\" bigint NOT NULL CHECK (\"ExpectedSizeBytes\" BETWEEN 1 AND 10485760), \"AudioSizeBytes\" bigint CHECK (\"AudioSizeBytes\" BETWEEN 1 AND 10485760), " +
        "\"AudioDurationSeconds\" integer CHECK (\"AudioDurationSeconds\" BETWEEN 1 AND 300), \"CreatedAt\" timestamptz NOT NULL, \"UpdatedAt\" timestamptz NOT NULL, " +
        "\"UploadExpiresAt\" timestamptz NOT NULL, \"AudioFinalizedAt\" timestamptz, \"AudioFinalizingAt\" timestamptz, \"ProcessedAt\" timestamptz, " +
        "\"AudioDeleteAfter\" timestamptz, \"AudioDeletedAt\" timestamptz, \"AudioDeleteRetryAt\" timestamptz, \"AudioCleanupClaimedAt\" timestamptz, " +
        "\"AudioDeleteAttemptCount\" integer NOT NULL DEFAULT 0 CHECK (\"AudioDeleteAttemptCount\" >= 0), \"AudioDeleteLastError\" varchar(80), " +
        "\"Status\" varchar(24) NOT NULL CHECK (\"Status\" IN ('AwaitingUpload','Pending','Processing','DraftReady','Unavailable','Confirmed','Cancelled')), " +
        "\"ActivityType\" varchar(24), \"OccurredAt\" timestamptz, \"Details\" varchar(4000), \"UncertainFieldsJson\" jsonb NOT NULL, \"Confidence\" double precision CHECK (\"Confidence\" BETWEEN 0 AND 1), " +
        "\"FailureCode\" varchar(80), \"RetryAt\" timestamptz, \"ProcessingAttemptCount\" integer NOT NULL DEFAULT 0 CHECK (\"ProcessingAttemptCount\" BETWEEN 0 AND 3), " +
        "\"Version\" bigint NOT NULL, \"StatusVersion\" bigint NOT NULL, \"NotifiedStatusVersion\" bigint NOT NULL, \"ConfirmedByUserId\" varchar(450), \"ConfirmedAt\" timestamptz, \"LogbookEntryId\" uuid, " +
        "UNIQUE (\"FarmId\", \"Id\"), CHECK (\"ConsentActorUserId\" = \"SubmittedByUserId\"), " +
        "CHECK ((\"ConsentedToExternalProcessing\" AND \"ConsentRevokedAt\" IS NULL) OR (NOT \"ConsentedToExternalProcessing\" AND \"ConsentRevokedAt\" IS NOT NULL)), " +
        "CHECK ((\"Status\" IN ('AwaitingUpload','Pending','Processing','DraftReady','Unavailable','Confirmed','Cancelled'))), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, " +
        "FOREIGN KEY (\"FarmId\", \"SubmittedByUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"WorkTaskId\") REFERENCES agro.work_tasks (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"ParcelId\") REFERENCES agro.parcels (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"LogbookEntryId\") REFERENCES agro.logbook_entries (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE UNIQUE INDEX \"IX_voice_activity_drafts_AudioObjectKey\" ON agro.voice_activity_drafts (\"AudioObjectKey\") WHERE \"AudioObjectKey\" IS NOT NULL; " +
        "CREATE UNIQUE INDEX \"IX_voice_activity_drafts_UploadObjectKey\" ON agro.voice_activity_drafts (\"UploadObjectKey\"); " +
        "CREATE INDEX \"IX_voice_activity_drafts_FarmId_Status_CreatedAt\" ON agro.voice_activity_drafts (\"FarmId\", \"Status\", \"CreatedAt\"); " +
        "CREATE INDEX \"IX_voice_activity_drafts_Status_RetryAt\" ON agro.voice_activity_drafts (\"Status\", \"RetryAt\"); " +
        "CREATE INDEX \"IX_voice_activity_drafts_AudioDeleteAfter_AudioDeleteRetryAt_AudioDeletedAt\" ON agro.voice_activity_drafts (\"AudioDeleteAfter\", \"AudioDeleteRetryAt\", \"AudioDeletedAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.voice_activity_drafts;");
}
