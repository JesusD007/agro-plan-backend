using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000015_CropCalendars")]
public sealed class CropCalendars : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.crop_calendar_templates (" +
        "\"Id\" uuid PRIMARY KEY, \"CropCatalogEntryId\" uuid NOT NULL REFERENCES agro.crop_catalog (\"Id\") ON DELETE RESTRICT, " +
        "\"Version\" varchar(80) NOT NULL, \"Source\" varchar(500) NOT NULL, \"SourceVersion\" varchar(200) NOT NULL, " +
        "\"Status\" varchar(24) NOT NULL, \"Region\" varchar(160), \"CultivationMethod\" varchar(120), " +
        "\"EffectiveFrom\" timestamptz NOT NULL, \"EffectiveUntil\" timestamptz, \"HarvestWindowStartDays\" integer, " +
        "\"HarvestWindowEndDays\" integer, \"TasksJson\" jsonb NOT NULL, " +
        "CHECK (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\"), " +
        "CHECK ((\"HarvestWindowStartDays\" IS NULL AND \"HarvestWindowEndDays\" IS NULL) OR (\"HarvestWindowStartDays\" >= 0 AND \"HarvestWindowEndDays\" >= \"HarvestWindowStartDays\")), " +
        "CHECK (\"Status\" <> 'Validated' OR (\"Source\" <> '' AND \"SourceVersion\" <> ''))); " +
        "CREATE INDEX \"IX_crop_calendar_templates_crop_status_validity\" ON agro.crop_calendar_templates (\"CropCatalogEntryId\", \"Status\", \"EffectiveFrom\", \"EffectiveUntil\"); " +
        "CREATE FUNCTION agro.reject_crop_calendar_template_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Crop calendar templates are immutable; add a new version'; END; $$; " +
        "CREATE TRIGGER \"TR_crop_calendar_templates_immutable\" BEFORE UPDATE OR DELETE ON agro.crop_calendar_templates FOR EACH ROW EXECUTE FUNCTION agro.reject_crop_calendar_template_mutation();");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS \"TR_crop_calendar_templates_immutable\" ON agro.crop_calendar_templates; " +
        "DROP FUNCTION IF EXISTS agro.reject_crop_calendar_template_mutation(); DROP TABLE IF EXISTS agro.crop_calendar_templates;");
}
