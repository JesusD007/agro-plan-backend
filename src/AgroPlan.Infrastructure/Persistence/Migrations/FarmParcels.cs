using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000003_FarmParcels")]
public sealed class FarmParcels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "CREATE TABLE agro.parcels (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"FarmId\" uuid NOT NULL REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, " +
            "\"Name\" character varying(160) NOT NULL, " +
            "\"Location\" character varying(240), " +
            "\"AreaHectares\" numeric(14,4) NOT NULL CHECK (\"AreaHectares\" > 0), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL, " +
            "UNIQUE (\"FarmId\", \"Id\"), UNIQUE (\"FarmId\", \"Name\"));\n" +
            "CREATE INDEX \"IX_parcels_FarmId\" ON agro.parcels (\"FarmId\");");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.parcels;");
}
