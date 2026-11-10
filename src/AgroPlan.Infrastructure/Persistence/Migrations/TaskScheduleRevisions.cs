using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000023_TaskScheduleRevisions")]
public sealed class TaskScheduleRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.task_schedule_revisions (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, " +
        "\"FarmId\" uuid NOT NULL, " +
        "\"WorkTaskId\" uuid NOT NULL, " +
        "\"SourceEventId\" uuid, " +
        "\"SourceRecordId\" uuid, " +
        "\"SourceRecordVersion\" integer NOT NULL CHECK (\"SourceRecordVersion\" >= 0), " +
        "\"TaskVersion\" integer NOT NULL CHECK (\"TaskVersion\" > 0), " +
        "\"PreviousDate\" date NOT NULL, " +
        "\"ProposedDate\" date NOT NULL CHECK (\"ProposedDate\" <> \"PreviousDate\"), " +
        "\"Explanation\" character varying(1000) NOT NULL, " +
        "\"Status\" character varying(16) NOT NULL CHECK (\"Status\" IN ('Proposed','Confirmed','Stale')), " +
        "\"ProposedAt\" timestamp with time zone NOT NULL, " +
        "\"ConfirmedByUserId\" character varying(450), " +
        "\"ConfirmedAt\" timestamp with time zone, " +
        "\"NotifiedAt\" timestamp with time zone, " +
        "CHECK ((\"SourceEventId\" IS NULL AND \"SourceRecordId\" IS NULL AND \"SourceRecordVersion\" = 0) OR " +
        "(\"SourceEventId\" IS NOT NULL AND \"SourceRecordId\" IS NOT NULL AND \"SourceRecordVersion\" > 0)), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, " +
        "FOREIGN KEY (\"FarmId\", \"WorkTaskId\") REFERENCES agro.work_tasks (\"FarmId\", \"Id\") ON DELETE CASCADE, " +
        "FOREIGN KEY (\"SourceEventId\") REFERENCES infra.outbox_messages (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"ConfirmedByUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT);" +
        "CREATE UNIQUE INDEX \"IX_task_schedule_revisions_FarmId_SourceEventId_WorkTaskId\" " +
        "ON agro.task_schedule_revisions (\"FarmId\", \"SourceEventId\", \"WorkTaskId\") WHERE \"SourceEventId\" IS NOT NULL;" +
        "CREATE INDEX \"IX_task_schedule_revisions_FarmId_WorkTaskId_ProposedAt\" " +
        "ON agro.task_schedule_revisions (\"FarmId\", \"WorkTaskId\", \"ProposedAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.task_schedule_revisions;");
}
