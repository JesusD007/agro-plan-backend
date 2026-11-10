using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000006_SowingJobsOutbox")]
public sealed class SowingJobsOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            "CREATE SCHEMA IF NOT EXISTS infra;\n" +
            "CREATE TABLE agro.sowing_proposals (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"FarmId\" uuid NOT NULL REFERENCES agro.farms (\"Id\") ON DELETE RESTRICT, " +
            "\"CropCycleId\" uuid NOT NULL, " +
            "\"SubmittedByUserId\" character varying(450) NOT NULL REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
            "\"InputSnapshotJson\" jsonb NOT NULL, \"ResultJson\" jsonb, " +
            "\"Status\" character varying(24) NOT NULL, " +
            "\"FailureCode\" character varying(80), \"FailureSummary\" character varying(400), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, \"UpdatedAt\" timestamp with time zone NOT NULL, " +
            "\"StatusChangedAt\" timestamp with time zone NOT NULL, \"StatusVersion\" bigint NOT NULL, \"NotifiedStatusVersion\" bigint NOT NULL, " +
            "UNIQUE (\"FarmId\", \"Id\"), " +
            "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT);\n" +
            "CREATE INDEX \"IX_sowing_proposals_FarmId_Status_CreatedAt\" ON agro.sowing_proposals (\"FarmId\", \"Status\", \"CreatedAt\");\n" +
            "CREATE INDEX \"IX_sowing_proposals_NotifiedStatusVersion_StatusChangedAt\" ON agro.sowing_proposals (\"NotifiedStatusVersion\", \"StatusChangedAt\");\n" +
            "CREATE TABLE infra.outbox_messages (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, \"AggregateId\" uuid NOT NULL, \"EventType\" character varying(120) NOT NULL, " +
            "\"PayloadJson\" jsonb NOT NULL, \"OccurredAt\" timestamp with time zone NOT NULL, \"AvailableAt\" timestamp with time zone NOT NULL, " +
            "\"LockedUntil\" timestamp with time zone, \"PublishedAt\" timestamp with time zone, \"DeadLetteredAt\" timestamp with time zone, " +
            "\"AttemptCount\" integer NOT NULL, \"LastError\" character varying(80));\n" +
            "CREATE INDEX \"IX_outbox_messages_AvailableAt_OccurredAt\" ON infra.outbox_messages (\"AvailableAt\", \"OccurredAt\") " +
            "WHERE \"PublishedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL;");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS infra.outbox_messages; DROP TABLE IF EXISTS agro.sowing_proposals;");
}
