using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000014_AdvisorAccessGrants")]
public sealed class AdvisorAccessGrants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.advisor_access_grants (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"AdvisorUserId\" varchar(450) NOT NULL, " +
        "\"Scopes\" integer[] NOT NULL, \"GrantedByUserId\" varchar(450) NOT NULL, \"Purpose\" varchar(500) NOT NULL, " +
        "\"StartsAt\" timestamptz NOT NULL, \"ExpiresAt\" timestamptz NOT NULL, \"CreatedAt\" timestamptz NOT NULL, " +
        "\"RevokedAt\" timestamptz, \"RevokedByUserId\" varchar(450), \"LastUsedAt\" timestamptz, " +
        "UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"AdvisorUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"GrantedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"RevokedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "CHECK (cardinality(\"Scopes\") > 0), CHECK (\"StartsAt\" < \"ExpiresAt\"), CHECK (\"ExpiresAt\" - \"StartsAt\" <= interval '90 days')); " +
        "CREATE INDEX \"IX_advisor_access_grants_FarmId_AdvisorUserId_ExpiresAt\" ON agro.advisor_access_grants (\"FarmId\", \"AdvisorUserId\", \"ExpiresAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.advisor_access_grants;");
}
