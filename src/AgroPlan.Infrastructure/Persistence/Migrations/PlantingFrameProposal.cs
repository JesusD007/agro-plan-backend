using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000010_PlantingFrameProposal")]
public sealed class PlantingFrameProposal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.crop_frameworks (\"Id\" uuid NOT NULL PRIMARY KEY, \"CropCatalogEntryId\" uuid NOT NULL REFERENCES agro.crop_catalog (\"Id\") ON DELETE CASCADE, \"Name\" varchar(120) NOT NULL, \"PlantSpacingMeters\" numeric(12,4), \"RowSpacingMeters\" numeric(12,4), \"AisleAllowanceRatio\" numeric(8,6), \"SourceName\" varchar(240), \"SourceUrl\" varchar(2048), \"ValidationStatus\" varchar(32) NOT NULL, \"Region\" varchar(160), \"ValidFrom\" timestamptz, \"ValidTo\" timestamptz, CHECK (\"ValidationStatus\" <> 'Validated' OR (\"PlantSpacingMeters\" > 0 AND \"RowSpacingMeters\" > 0 AND \"AisleAllowanceRatio\" >= 0 AND \"AisleAllowanceRatio\" < 1 AND \"SourceName\" IS NOT NULL)), CHECK (\"ValidTo\" IS NULL OR \"ValidFrom\" IS NULL OR \"ValidTo\" >= \"ValidFrom\")); " +
        "CREATE INDEX \"IX_crop_frameworks_CropCatalogEntryId_ValidationStatus\" ON agro.crop_frameworks (\"CropCatalogEntryId\", \"ValidationStatus\"); " +
        "ALTER TABLE agro.crop_cycles ADD COLUMN \"SelectedFrameworkId\" uuid REFERENCES agro.crop_frameworks (\"Id\") ON DELETE RESTRICT; " +
        "CREATE TABLE agro.planting_frame_proposals (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"CropCycleId\" uuid NOT NULL, \"SubmittedByUserId\" varchar(450) NOT NULL, \"InputSnapshotJson\" jsonb NOT NULL, \"ResultJson\" jsonb, \"Status\" varchar(32) NOT NULL, \"FailureCode\" varchar(80), \"FailureSummary\" varchar(600), \"CreatedAt\" timestamptz NOT NULL, \"UpdatedAt\" timestamptz NOT NULL, \"ConfirmedByUserId\" varchar(450), \"ConfirmedAt\" timestamptz, \"SelectedFrameworkId\" uuid REFERENCES agro.crop_frameworks (\"Id\"), CHECK (\"Status\" <> 'Confirmed' OR (\"SelectedFrameworkId\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"ConfirmedAt\" IS NOT NULL))); " +
        "CREATE INDEX \"IX_planting_frame_proposals_FarmId_CreatedAt\" ON agro.planting_frame_proposals (\"FarmId\", \"CreatedAt\"); CREATE INDEX \"IX_planting_frame_proposals_Status_UpdatedAt\" ON agro.planting_frame_proposals (\"Status\", \"UpdatedAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE IF EXISTS agro.planting_frame_proposals; ALTER TABLE agro.crop_cycles DROP COLUMN IF EXISTS \"SelectedFrameworkId\"; DROP TABLE IF EXISTS agro.crop_frameworks;");
}
