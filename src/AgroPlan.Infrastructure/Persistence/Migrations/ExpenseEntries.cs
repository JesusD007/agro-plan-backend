using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000009_ExpenseEntries")]
public sealed class ExpenseEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.expense_entries (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"ParcelId\" uuid NOT NULL, \"CropCycleId\" uuid, " +
        "\"Category\" varchar(24) NOT NULL, \"OccurredAt\" timestamptz NOT NULL, \"Description\" varchar(1000) NOT NULL, " +
        "\"Amount\" numeric(18,4) NOT NULL CHECK (\"Amount\" >= 0), \"CurrencyCode\" varchar(3) NOT NULL CHECK (char_length(\"CurrencyCode\") = 3), " +
        "\"Quantity\" numeric(18,4) CHECK (\"Quantity\" IS NULL OR \"Quantity\" >= 0), \"Unit\" varchar(64), " +
        "\"ActorUserId\" varchar(450) NOT NULL, \"CreatedAt\" timestamptz NOT NULL, \"CorrectsEntryId\" uuid, \"CorrectionReason\" varchar(1000), " +
        "CHECK ((\"Quantity\" IS NULL) = (\"Unit\" IS NULL)), UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\", \"ParcelId\") REFERENCES agro.parcels (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CorrectsEntryId\") REFERENCES agro.expense_entries (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_expense_entries_FarmId_OccurredAt\" ON agro.expense_entries (\"FarmId\", \"OccurredAt\"); " +
        "CREATE FUNCTION agro.reject_expense_entry_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Expense entries are immutable'; END; $$; " +
        "CREATE TRIGGER \"TR_expense_entries_immutable\" BEFORE UPDATE OR DELETE ON agro.expense_entries FOR EACH ROW EXECUTE FUNCTION agro.reject_expense_entry_mutation();");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TRIGGER IF EXISTS \"TR_expense_entries_immutable\" ON agro.expense_entries; " +
        "DROP FUNCTION IF EXISTS agro.reject_expense_entry_mutation(); DROP TABLE IF EXISTS agro.expense_entries;");
}
