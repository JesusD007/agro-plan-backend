using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261111000028_SymptomPhotoDimensions")]
public sealed class SymptomPhotoDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.symptom_report_photos " +
        "ADD COLUMN \"WidthPixels\" integer CHECK (\"WidthPixels\" IS NULL OR \"WidthPixels\" BETWEEN 1 AND 8192), " +
        "ADD COLUMN \"HeightPixels\" integer CHECK (\"HeightPixels\" IS NULL OR \"HeightPixels\" BETWEEN 1 AND 8192), " +
        "ADD CONSTRAINT \"CK_symptom_report_photos_PixelCount\" CHECK (\"WidthPixels\" IS NULL OR \"HeightPixels\" IS NULL OR \"WidthPixels\"::bigint * \"HeightPixels\" <= 20000000);");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.symptom_report_photos DROP CONSTRAINT IF EXISTS \"CK_symptom_report_photos_PixelCount\"; " +
        "ALTER TABLE agro.symptom_report_photos DROP COLUMN IF EXISTS \"WidthPixels\", DROP COLUMN IF EXISTS \"HeightPixels\";");
}
