using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261111000026_SymptomReports")]
public sealed class SymptomReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
        "CREATE TABLE agro.symptom_reports (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"SubmittedByUserId\" varchar(450) NOT NULL, \"CropCycleId\" uuid, " +
        "\"Notes\" varchar(2000), \"Status\" varchar(16) NOT NULL CHECK (\"Status\" IN ('Uploading','Pending','Processing','Closed','Rejected')), " +
        "\"CreatedAt\" timestamptz NOT NULL, \"SubmittedAt\" timestamptz, \"ProcessingStartedAt\" timestamptz, \"ObservedAt\" timestamptz, " +
        "\"ProcessingAttempts\" integer NOT NULL DEFAULT 0 CHECK (\"ProcessingAttempts\" >= 0), \"Version\" integer NOT NULL CHECK (\"Version\" > 0), " +
        "\"VisibleSigns\" varchar(4000), \"Uncertainty\" varchar(2000), \"ImageSufficient\" boolean, \"RequestedEvidence\" text[] NOT NULL DEFAULT '{}', " +
        "\"HumanDiagnosis\" varchar(2000), \"HumanTreatment\" varchar(4000), \"ReviewedByUserId\" varchar(450), \"ReviewedAt\" timestamptz, " +
        "UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"SubmittedByUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"ReviewedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT); " +
        "CREATE TABLE agro.symptom_report_photos (" +
        "\"ReportId\" uuid NOT NULL, \"Id\" uuid NOT NULL, \"UploadObjectKey\" varchar(1024) NOT NULL, \"ObjectKey\" varchar(1024), " +
        "\"ContentType\" varchar(64) NOT NULL CHECK (\"ContentType\" IN ('image/jpeg','image/png','image/webp')), " +
        "\"ExpectedSizeBytes\" bigint NOT NULL CHECK (\"ExpectedSizeBytes\" BETWEEN 1 AND 10485760), \"SizeBytes\" bigint CHECK (\"SizeBytes\" BETWEEN 1 AND 10485760), " +
        "\"ConsentActorUserId\" varchar(450) NOT NULL, \"ConsentGranted\" boolean NOT NULL, \"ConsentNoticeVersion\" varchar(80), " +
        "\"ConsentRecordedAt\" timestamptz NOT NULL, \"ConsentRevokedAt\" timestamptz, \"ExpiresAt\" timestamptz NOT NULL, \"FinalizedAt\" timestamptz, " +
        "\"DeleteAfter\" timestamptz, \"DeleteRetryAt\" timestamptz, \"DeleteClaimedAt\" timestamptz, \"DeletedAt\" timestamptz, " +
        "\"DeleteAttempts\" integer NOT NULL DEFAULT 0 CHECK (\"DeleteAttempts\" >= 0), \"DeleteLastError\" varchar(80), " +
        "PRIMARY KEY (\"ReportId\", \"Id\"), UNIQUE (\"UploadObjectKey\"), " +
        "CHECK (NOT \"ConsentGranted\" OR (\"ConsentNoticeVersion\" IS NOT NULL AND \"ConsentRevokedAt\" IS NULL)), " +
        "FOREIGN KEY (\"ReportId\") REFERENCES agro.symptom_reports (\"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_symptom_reports_FarmId_Status_CreatedAt\" ON agro.symptom_reports (\"FarmId\", \"Status\", \"CreatedAt\"); " +
        "CREATE INDEX \"IX_symptom_reports_Status_ProcessingStartedAt\" ON agro.symptom_reports (\"Status\", \"ProcessingStartedAt\"); " +
        "CREATE INDEX \"IX_symptom_report_photos_DeleteAfter_DeleteRetryAt_DeletedAt\" ON agro.symptom_report_photos (\"DeleteAfter\", \"DeleteRetryAt\", \"DeletedAt\"); " +
        "CREATE TABLE agro.symptom_report_decisions (" +
        "\"ReportId\" uuid NOT NULL, \"Id\" uuid NOT NULL, \"Action\" varchar(32) NOT NULL CHECK (\"Action\" IN ('MoreEvidenceRequested','Confirmed','Rejected')), " +
        "\"ReviewerUserId\" varchar(450) NOT NULL, \"DecidedAt\" timestamptz NOT NULL, \"RequestedEvidence\" varchar(2000), " +
        "\"Diagnosis\" varchar(2000), \"Treatment\" varchar(4000), PRIMARY KEY (\"ReportId\", \"Id\"), " +
        "FOREIGN KEY (\"ReportId\") REFERENCES agro.symptom_reports (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"ReviewerUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_symptom_report_decisions_ReportId_DecidedAt\" ON agro.symptom_report_decisions (\"ReportId\", \"DecidedAt\");");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.symptom_report_decisions; DROP TABLE IF EXISTS agro.symptom_report_photos; DROP TABLE IF EXISTS agro.symptom_reports;");
}
