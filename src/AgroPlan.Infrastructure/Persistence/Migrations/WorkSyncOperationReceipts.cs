using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000019_WorkSyncOperationReceipts")]
public sealed class WorkSyncOperationReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.sync_operation_receipts (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"WorkerUserId\" character varying(450) NOT NULL, \"ClientOperationId\" uuid NOT NULL, \"PayloadFingerprint\" character varying(64) NOT NULL CHECK (length(\"PayloadFingerprint\") = 64), \"Status\" character varying(24) NOT NULL CHECK (\"Status\" IN ('Applied','Conflict')), \"ResultJson\" jsonb NOT NULL, \"RecordedAt\" timestamp with time zone NOT NULL, FOREIGN KEY (\"FarmId\", \"WorkerUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT);\n" +
        "CREATE UNIQUE INDEX \"IX_sync_operation_receipts_FarmId_WorkerUserId_ClientOperationId\" ON agro.sync_operation_receipts (\"FarmId\", \"WorkerUserId\", \"ClientOperationId\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.sync_operation_receipts;");
}
