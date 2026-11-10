using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000005_CropCycles")]
public sealed class CropCycles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            "CREATE TABLE agro.crop_cycles (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"FarmId\" uuid NOT NULL REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, " +
            "\"ParcelId\" uuid NOT NULL, " +
            "\"CropCatalogEntryId\" uuid NOT NULL REFERENCES agro.crop_catalog (\"Id\") ON DELETE RESTRICT, " +
            "\"DesiredPlantingDate\" date, " +
            "\"Variety\" character varying(120), " +
            "\"Notes\" character varying(2000), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"UpdatedAt\" timestamp with time zone NOT NULL, " +
            "UNIQUE (\"FarmId\", \"Id\"), " +
            "FOREIGN KEY (\"FarmId\", \"ParcelId\") REFERENCES agro.parcels (\"FarmId\", \"Id\") ON DELETE RESTRICT);\n" +
            "CREATE INDEX \"IX_crop_cycles_FarmId_ParcelId\" ON agro.crop_cycles (\"FarmId\", \"ParcelId\");\n" +
            "CREATE INDEX \"IX_crop_cycles_FarmId_DesiredPlantingDate\" ON agro.crop_cycles (\"FarmId\", \"DesiredPlantingDate\");");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.crop_cycles;");
}
