using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000002_FarmAdministration")]
public sealed class FarmAdministration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "CREATE TABLE agro.farm_membership_audit (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"FarmId\" uuid NOT NULL, " +
            "\"TargetUserId\" character varying(450) NOT NULL, " +
            "\"ActorUserId\" character varying(450) NOT NULL, " +
            "\"Action\" character varying(32) NOT NULL CHECK (\"Action\" IN ('admin_granted', 'admin_revoked')), " +
            "\"OccurredAt\" timestamp with time zone NOT NULL);\n" +
            "CREATE INDEX \"IX_farm_membership_audit_FarmId_OccurredAt\" ON agro.farm_membership_audit (\"FarmId\", \"OccurredAt\");\n" +
            "CREATE INDEX \"IX_farm_membership_audit_TargetUserId_OccurredAt\" ON agro.farm_membership_audit (\"TargetUserId\", \"OccurredAt\");");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.farm_membership_audit;");
}
