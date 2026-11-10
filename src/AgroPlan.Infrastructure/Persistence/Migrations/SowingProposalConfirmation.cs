using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000007_SowingProposalConfirmation")]
public sealed class SowingProposalConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.sowing_proposals ADD COLUMN \"ConfirmedByUserId\" character varying(450), " +
        "ADD COLUMN \"ConfirmedAt\" timestamp with time zone;");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.sowing_proposals DROP COLUMN \"ConfirmedByUserId\", DROP COLUMN \"ConfirmedAt\";");
}
