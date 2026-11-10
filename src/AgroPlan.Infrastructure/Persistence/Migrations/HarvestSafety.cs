using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000014_HarvestSafety")]
public sealed class HarvestSafety : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.validated_product_labels (" +
        "\"Id\" uuid PRIMARY KEY, \"ProductRegistrationId\" varchar(120) NOT NULL, \"FormulationId\" varchar(120) NOT NULL, " +
        "\"CropId\" varchar(120) NOT NULL, \"IntendedUseId\" varchar(120) NOT NULL, \"Jurisdiction\" varchar(8) NOT NULL, " +
        "\"Source\" varchar(500) NOT NULL, \"SourceVersion\" varchar(200) NOT NULL, \"EffectiveFrom\" timestamptz NOT NULL, \"EffectiveUntil\" timestamptz, " +
        "\"PreHarvestIntervalDays\" integer, \"MaximumResidueLimit\" numeric(18,6), \"WorkerReEntryIntervalHours\" integer, \"Validated\" boolean NOT NULL, " +
        "CHECK (\"PreHarvestIntervalDays\" IS NULL OR \"PreHarvestIntervalDays\" >= 0), CHECK (\"MaximumResidueLimit\" IS NULL OR \"MaximumResidueLimit\" >= 0), " +
        "CHECK (\"WorkerReEntryIntervalHours\" IS NULL OR \"WorkerReEntryIntervalHours\" >= 0), CHECK (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")); " +
        "CREATE INDEX \"IX_validated_product_labels_key\" ON agro.validated_product_labels (\"ProductRegistrationId\", \"FormulationId\", \"CropId\", \"IntendedUseId\", \"Jurisdiction\"); " +
        "CREATE FUNCTION agro.reject_validated_product_label_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Validated product labels are immutable; add a new version'; END; $$; " +
        "CREATE TRIGGER \"TR_validated_product_labels_immutable\" BEFORE UPDATE OR DELETE ON agro.validated_product_labels FOR EACH ROW EXECUTE FUNCTION agro.reject_validated_product_label_mutation(); " +
        "CREATE TABLE agro.harvest_entries (\"Id\" uuid PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"ParcelId\" uuid NOT NULL, \"CropCycleId\" uuid NOT NULL, " +
        "\"HarvestedAt\" timestamptz NOT NULL, \"Quantity\" numeric(18,4) NOT NULL CHECK (\"Quantity\" > 0), \"Unit\" varchar(64) NOT NULL, " +
        "\"ActorUserId\" varchar(450) NOT NULL, \"CreatedAt\" timestamptz NOT NULL, \"AssessmentEvidenceJson\" jsonb NOT NULL, " +
        "\"CorrectsEntryId\" uuid, \"CorrectionReason\" varchar(1000), UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\", \"ParcelId\") REFERENCES agro.parcels (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CorrectsEntryId\") REFERENCES agro.harvest_entries (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_harvest_entries_FarmId_HarvestedAt\" ON agro.harvest_entries (\"FarmId\", \"HarvestedAt\"); " +
        "CREATE FUNCTION agro.reject_harvest_entry_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Harvest entries are immutable'; END; $$; " +
        "CREATE TRIGGER \"TR_harvest_entries_immutable\" BEFORE UPDATE OR DELETE ON agro.harvest_entries FOR EACH ROW EXECUTE FUNCTION agro.reject_harvest_entry_mutation();");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS \"TR_harvest_entries_immutable\" ON agro.harvest_entries; " +
        "DROP TRIGGER IF EXISTS \"TR_validated_product_labels_immutable\" ON agro.validated_product_labels; " +
        "DROP FUNCTION IF EXISTS agro.reject_harvest_entry_mutation(); DROP FUNCTION IF EXISTS agro.reject_validated_product_label_mutation(); DROP TABLE IF EXISTS agro.harvest_entries; " +
        "DROP TABLE IF EXISTS agro.validated_product_labels;");
}
