using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261111000029_WeeklySummaryNarrativeRetries")]
public sealed class WeeklySummaryNarrativeRetries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.weekly_summaries " +
        "ADD COLUMN \"NarrativeAttempts\" integer NOT NULL DEFAULT 0 CHECK (\"NarrativeAttempts\" >= 0), " +
        "ADD COLUMN \"NarrativeProcessingStartedAt\" timestamptz;");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.weekly_summaries DROP COLUMN IF EXISTS \"NarrativeProcessingStartedAt\", " +
        "DROP COLUMN IF EXISTS \"NarrativeAttempts\";");
}
