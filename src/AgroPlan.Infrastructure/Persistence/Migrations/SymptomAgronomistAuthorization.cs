using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261111000027_SymptomAgronomistAuthorization")]
public sealed class SymptomAgronomistAuthorization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.trusted_agronomist_verifications (\"UserId\" varchar(450) PRIMARY KEY, \"VerifiedByUserId\" varchar(450) NOT NULL, " +
        "\"CredentialReference\" varchar(300) NOT NULL, \"VerifiedAt\" timestamptz NOT NULL, \"RevokedByUserId\" varchar(450), \"RevokedAt\" timestamptz, " +
        "FOREIGN KEY (\"UserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, FOREIGN KEY (\"VerifiedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"RevokedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT); " +
        "CREATE TABLE agro.farm_agronomist_authorizations (\"FarmId\" uuid NOT NULL, \"AgronomistUserId\" varchar(450) NOT NULL, \"GrantedByUserId\" varchar(450) NOT NULL, " +
        "\"GrantedAt\" timestamptz NOT NULL, \"RevokedByUserId\" varchar(450), \"RevokedAt\" timestamptz, PRIMARY KEY (\"FarmId\", \"AgronomistUserId\"), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE RESTRICT, FOREIGN KEY (\"AgronomistUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"GrantedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, FOREIGN KEY (\"RevokedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT);");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.farm_agronomist_authorizations; DROP TABLE IF EXISTS agro.trusted_agronomist_verifications;");
}
