using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000013_InputApplications")]
public sealed class InputApplications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.input_application_entries (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"ParcelId\" uuid NOT NULL, \"CropCycleId\" uuid, " +
        "\"ProductRegistrationId\" varchar(120) NOT NULL, \"FormulationId\" varchar(120) NOT NULL, \"CropId\" varchar(120) NOT NULL, \"IntendedUseId\" varchar(120) NOT NULL, " +
        "\"AppliedAt\" timestamptz NOT NULL, \"Method\" varchar(16) NOT NULL, \"Quantity\" numeric(18,4) NOT NULL, \"Unit\" varchar(64) NOT NULL, " +
        "\"ActorUserId\" varchar(450) NOT NULL, \"CreatedAt\" timestamptz NOT NULL, \"CorrectsEntryId\" uuid, \"CorrectionReason\" varchar(1000), UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\", \"ParcelId\") REFERENCES agro.parcels (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CorrectsEntryId\") REFERENCES agro.input_application_entries (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_input_application_entries_FarmId_AppliedAt\" ON agro.input_application_entries (\"FarmId\", \"AppliedAt\"); " +
        "CREATE INDEX \"IX_input_application_entries_frequency\" ON agro.input_application_entries (\"FarmId\", \"ProductRegistrationId\", \"FormulationId\", \"CropId\", \"IntendedUseId\", \"AppliedAt\"); " +
        "CREATE FUNCTION agro.reject_input_application_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Input application entries are immutable'; END; $$; " +
        "CREATE TRIGGER \"TR_input_application_entries_immutable\" BEFORE UPDATE OR DELETE ON agro.input_application_entries FOR EACH ROW EXECUTE FUNCTION agro.reject_input_application_mutation(); " +
        "CREATE TABLE agro.validated_application_frequency_rules (\"Id\" uuid PRIMARY KEY, \"ProductRegistrationId\" varchar(120) NOT NULL, \"FormulationId\" varchar(120) NOT NULL, " +
        "\"CropId\" varchar(120) NOT NULL, \"IntendedUseId\" varchar(120) NOT NULL, \"Jurisdiction\" varchar(8) NOT NULL, \"Source\" varchar(500) NOT NULL, \"SourceVersion\" varchar(200) NOT NULL, " +
        "\"ValidFrom\" timestamptz NOT NULL, \"ValidUntil\" timestamptz, \"PeriodDays\" integer NOT NULL CHECK (\"PeriodDays\" > 0), \"MaximumApplications\" integer NOT NULL CHECK (\"MaximumApplications\" > 0), " +
        "\"ApproachingAt\" integer, \"Validated\" boolean NOT NULL, CHECK (\"ApproachingAt\" IS NULL OR (\"ApproachingAt\" > 0 AND \"ApproachingAt\" < \"MaximumApplications\")), CHECK (\"ValidUntil\" IS NULL OR \"ValidUntil\" >= \"ValidFrom\")); " +
        "CREATE INDEX \"IX_validated_application_frequency_rules_key\" ON agro.validated_application_frequency_rules (\"ProductRegistrationId\", \"FormulationId\", \"CropId\", \"IntendedUseId\", \"Jurisdiction\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS \"TR_input_application_entries_immutable\" ON agro.input_application_entries; " +
        "DROP FUNCTION IF EXISTS agro.reject_input_application_mutation(); DROP TABLE IF EXISTS agro.input_application_entries; " +
        "DROP TABLE IF EXISTS agro.validated_application_frequency_rules;");
}
