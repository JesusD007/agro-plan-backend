using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000016_ScheduleChangeProposals")]
public sealed class ScheduleChangeProposals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.schedule_change_proposals (" +
        "\"Id\" uuid PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"CropCycleId\" uuid NOT NULL, \"Version\" integer NOT NULL, " +
        "\"EvidenceVersion\" varchar(200) NOT NULL, \"DeltasJson\" jsonb NOT NULL, \"EvidenceIdsJson\" jsonb NOT NULL, " +
        "\"ExplanationJson\" jsonb, \"ExplanationProvider\" varchar(80), \"ExplanationModel\" varchar(160), \"ExplanationPromptVersion\" varchar(80), \"ExplanationSchemaVersion\" varchar(80), " +
        "\"Status\" varchar(24) NOT NULL, \"FailureCode\" varchar(100), \"CreatedAt\" timestamptz NOT NULL, \"ExpiresAt\" timestamptz NOT NULL, " +
        "\"ConfirmedAt\" timestamptz, \"ConfirmedBy\" varchar(450), \"StatusVersion\" integer NOT NULL, UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_schedule_change_proposals_farm_cycle_created\" ON agro.schedule_change_proposals (\"FarmId\", \"CropCycleId\", \"CreatedAt\"); " +
        "CREATE INDEX \"IX_schedule_change_proposals_status_expiry\" ON agro.schedule_change_proposals (\"Status\", \"ExpiresAt\"); " +
        "CREATE FUNCTION agro.protect_schedule_proposal() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN " +
        "IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Schedule proposals are retained'; END IF; " +
        "IF NEW.\"Id\" <> OLD.\"Id\" OR NEW.\"FarmId\" <> OLD.\"FarmId\" OR NEW.\"CropCycleId\" <> OLD.\"CropCycleId\" OR NEW.\"Version\" <> OLD.\"Version\" OR NEW.\"EvidenceVersion\" <> OLD.\"EvidenceVersion\" OR NEW.\"DeltasJson\" <> OLD.\"DeltasJson\" OR NEW.\"EvidenceIdsJson\" <> OLD.\"EvidenceIdsJson\" OR NEW.\"CreatedAt\" <> OLD.\"CreatedAt\" OR NEW.\"ExpiresAt\" <> OLD.\"ExpiresAt\" THEN RAISE EXCEPTION 'Schedule proposal facts are immutable'; END IF; RETURN NEW; END; $$; " +
        "CREATE TRIGGER \"TR_schedule_proposals_immutable\" BEFORE UPDATE OR DELETE ON agro.schedule_change_proposals FOR EACH ROW EXECUTE FUNCTION agro.protect_schedule_proposal(); " +
        "CREATE TABLE agro.calendar_schedule_items (\"FarmId\" uuid NOT NULL, \"CropCycleId\" uuid NOT NULL, \"TaskKey\" varchar(120) NOT NULL, \"DueDate\" date NOT NULL, \"LastProposalId\" uuid, \"UpdatedAt\" timestamptz NOT NULL, " +
        "PRIMARY KEY (\"FarmId\", \"CropCycleId\", \"TaskKey\"), FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"LastProposalId\") REFERENCES agro.schedule_change_proposals (\"FarmId\", \"Id\") ON DELETE RESTRICT);");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS \"TR_schedule_proposals_immutable\" ON agro.schedule_change_proposals; " +
        "DROP TABLE IF EXISTS agro.calendar_schedule_items; DROP FUNCTION IF EXISTS agro.protect_schedule_proposal(); DROP TABLE IF EXISTS agro.schedule_change_proposals;");
}
