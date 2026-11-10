using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000003_WorkerInvitations")]
public sealed class WorkerInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.farms ADD COLUMN \"WorkerEntitlement\" character varying(24) NOT NULL DEFAULT 'Basic' CHECK (\"WorkerEntitlement\" IN ('Basic', 'Premium'));\n" +
        "CREATE TABLE agro.worker_invitations (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, \"Email\" character varying(256) NOT NULL, \"TokenDigest\" character varying(64) NOT NULL UNIQUE, \"IssuedByUserId\" character varying(450) NOT NULL REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, \"IssuedAt\" timestamp with time zone NOT NULL, \"ExpiresAt\" timestamp with time zone NOT NULL, \"AcceptedAt\" timestamp with time zone, \"AcceptedByUserId\" character varying(450) REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, \"CancelledAt\" timestamp with time zone);\n" +
        "CREATE INDEX \"IX_worker_invitations_FarmId_Email_ExpiresAt\" ON agro.worker_invitations (\"FarmId\", \"Email\", \"ExpiresAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.worker_invitations;\nALTER TABLE agro.farms DROP COLUMN IF EXISTS \"WorkerEntitlement\";");
}
