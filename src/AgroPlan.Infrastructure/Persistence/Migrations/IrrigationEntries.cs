using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000012_IrrigationEntries")]
public sealed class IrrigationEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.irrigation_entries (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"ParcelId\" uuid NOT NULL, \"CropCycleId\" uuid, " +
        "\"Method\" varchar(16) NOT NULL, \"WaterSource\" varchar(200) NOT NULL, \"OccurredAt\" timestamptz NOT NULL, \"ActorUserId\" varchar(450) NOT NULL, \"CreatedAt\" timestamptz NOT NULL, \"CorrectsEntryId\" uuid, \"CorrectionReason\" varchar(1000), " +
        "UNIQUE (\"FarmId\", \"Id\"), FOREIGN KEY (\"FarmId\", \"ParcelId\") REFERENCES agro.parcels (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CorrectsEntryId\") REFERENCES agro.irrigation_entries (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_irrigation_entries_FarmId_OccurredAt\" ON agro.irrigation_entries (\"FarmId\", \"OccurredAt\"); " +
        "CREATE FUNCTION agro.reject_irrigation_entry_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Irrigation entries are immutable'; END; $$; " +
        "CREATE TRIGGER \"TR_irrigation_entries_immutable\" BEFORE UPDATE OR DELETE ON agro.irrigation_entries FOR EACH ROW EXECUTE FUNCTION agro.reject_irrigation_entry_mutation();");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS \"TR_irrigation_entries_immutable\" ON agro.irrigation_entries; " +
        "DROP FUNCTION IF EXISTS agro.reject_irrigation_entry_mutation(); DROP TABLE IF EXISTS agro.irrigation_entries;");
}
