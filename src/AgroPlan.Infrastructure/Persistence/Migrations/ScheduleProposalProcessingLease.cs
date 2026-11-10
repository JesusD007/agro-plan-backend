using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000017_ScheduleProposalProcessingLease")]
public sealed class ScheduleProposalProcessingLease : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.schedule_change_proposals " +
        "ADD COLUMN \"ProcessingStartedAt\" timestamptz, " +
        "ADD COLUMN \"ProcessingAttempts\" integer NOT NULL DEFAULT 0;");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.schedule_change_proposals " +
        "DROP COLUMN IF EXISTS \"ProcessingStartedAt\", " +
        "DROP COLUMN IF EXISTS \"ProcessingAttempts\";");
}
