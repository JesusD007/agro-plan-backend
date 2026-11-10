using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000004_CropCatalog")]
public sealed class CropCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "CREATE TABLE agro.crop_catalog (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"Code\" character varying(40) NOT NULL UNIQUE, " +
            "\"DisplayName\" character varying(120) NOT NULL, " +
            "\"SourceName\" character varying(240) NOT NULL, " +
            "\"ValidationStatus\" character varying(32) NOT NULL, " +
            "\"Region\" character varying(160), " +
            "\"ValidFrom\" timestamp with time zone, " +
            "\"ValidTo\" timestamp with time zone, " +
            "CHECK (\"ValidTo\" IS NULL OR \"ValidFrom\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"));\n" +
            "CREATE TABLE agro.agronomic_parameters (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"CropCatalogEntryId\" uuid NOT NULL REFERENCES agro.crop_catalog (\"Id\") ON DELETE CASCADE, " +
            "\"Key\" character varying(80) NOT NULL, " +
            "\"NumericValue\" numeric(20,6), " +
            "\"Unit\" character varying(48), " +
            "\"ValidationStatus\" character varying(32) NOT NULL, " +
            "\"SourceName\" character varying(240), " +
            "\"SourceUrl\" character varying(2048), " +
            "\"Region\" character varying(160), " +
            "\"ValidFrom\" timestamp with time zone, " +
            "\"ValidTo\" timestamp with time zone, " +
            "UNIQUE (\"CropCatalogEntryId\", \"Key\"), " +
            "CHECK (\"ValidationStatus\" <> 'Validated' OR (\"NumericValue\" IS NOT NULL AND \"Unit\" IS NOT NULL AND \"SourceName\" IS NOT NULL)), " +
            "CHECK (\"ValidTo\" IS NULL OR \"ValidFrom\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"));\n" +
            "INSERT INTO agro.crop_catalog (\"Id\", \"Code\", \"DisplayName\", \"SourceName\", \"ValidationStatus\", \"Region\", \"ValidFrom\", \"ValidTo\") VALUES " +
            "('e1000000-0000-4000-8000-000000000001', 'PLATANO', 'Plátano', 'Lista de cultivos del piloto aprobada por el usuario', 'IdentityOnly', NULL, NULL, NULL), " +
            "('e1000000-0000-4000-8000-000000000002', 'YUCA', 'Yuca', 'Lista de cultivos del piloto aprobada por el usuario', 'IdentityOnly', NULL, NULL, NULL), " +
            "('e1000000-0000-4000-8000-000000000003', 'BATATA', 'Batata', 'Lista de cultivos del piloto aprobada por el usuario', 'IdentityOnly', NULL, NULL, NULL), " +
            "('e1000000-0000-4000-8000-000000000004', 'ARROZ', 'Arroz', 'Lista de cultivos del piloto aprobada por el usuario', 'IdentityOnly', NULL, NULL, NULL), " +
            "('e1000000-0000-4000-8000-000000000005', 'HABICHUELA', 'Habichuela', 'Lista de cultivos del piloto aprobada por el usuario', 'IdentityOnly', NULL, NULL, NULL);");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.agronomic_parameters; DROP TABLE IF EXISTS agro.crop_catalog;");
}
