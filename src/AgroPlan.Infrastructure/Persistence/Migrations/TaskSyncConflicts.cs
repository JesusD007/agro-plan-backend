using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000020_TaskSyncConflicts")]
public sealed class TaskSyncConflicts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.work_tasks ADD COLUMN \"Version\" integer NOT NULL DEFAULT 1 CHECK (\"Version\" > 0);\n" +
        "CREATE TABLE agro.task_sync_conflicts (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"WorkTaskId\" uuid NOT NULL, \"WorkerUserId\" character varying(450) NOT NULL, \"ClientOperationId\" uuid NOT NULL, \"BaseVersion\" integer NOT NULL CHECK (\"BaseVersion\" > 0), \"ClientValuesJson\" jsonb NOT NULL, \"ServerVersion\" integer NOT NULL CHECK (\"ServerVersion\" > \"BaseVersion\"), \"ServerValuesJson\" jsonb NOT NULL, \"DetectedAt\" timestamp with time zone NOT NULL, UNIQUE (\"FarmId\", \"Id\"), UNIQUE (\"FarmId\", \"WorkTaskId\", \"Id\"), FOREIGN KEY (\"FarmId\", \"WorkTaskId\") REFERENCES agro.work_tasks (\"FarmId\", \"Id\") ON DELETE CASCADE, FOREIGN KEY (\"FarmId\", \"WorkerUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT);\n" +
        "CREATE UNIQUE INDEX \"IX_task_sync_conflicts_FarmId_WorkerUserId_ClientOperationId\" ON agro.task_sync_conflicts (\"FarmId\", \"WorkerUserId\", \"ClientOperationId\");\n" +
        "CREATE INDEX \"IX_task_sync_conflicts_FarmId_WorkTaskId_DetectedAt\" ON agro.task_sync_conflicts (\"FarmId\", \"WorkTaskId\", \"DetectedAt\");\n" +
        "CREATE TABLE agro.task_sync_conflict_resolutions (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"ConflictId\" uuid NOT NULL, \"DecidedByUserId\" character varying(450) NOT NULL, \"Resolution\" character varying(16) NOT NULL CHECK (\"Resolution\" IN ('Server','Client','Edited')), \"SelectedValuesJson\" jsonb NOT NULL, \"ResultTaskVersion\" integer NOT NULL CHECK (\"ResultTaskVersion\" > 0), \"DecidedAt\" timestamp with time zone NOT NULL, FOREIGN KEY (\"FarmId\", \"ConflictId\") REFERENCES agro.task_sync_conflicts (\"FarmId\", \"Id\") ON DELETE CASCADE, FOREIGN KEY (\"FarmId\", \"DecidedByUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT);\n" +
        "CREATE UNIQUE INDEX \"IX_task_sync_conflict_resolutions_ConflictId\" ON agro.task_sync_conflict_resolutions (\"ConflictId\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.task_sync_conflict_resolutions;\n" +
        "DROP TABLE IF EXISTS agro.task_sync_conflicts;\n" +
        "ALTER TABLE agro.work_tasks DROP COLUMN IF EXISTS \"Version\";");
}
