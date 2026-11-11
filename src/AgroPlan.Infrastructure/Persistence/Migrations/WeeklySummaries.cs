using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261111000001_WeeklySummaries")]
public sealed class WeeklySummaries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.weekly_summaries (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"RequestedByUserId\" varchar(450) NOT NULL, " +
        "\"RequestedWeekAnchorDate\" date NOT NULL, \"WeekStartDate\" date NOT NULL, \"WeekEndDate\" date NOT NULL, " +
        "\"CropCycleId\" uuid, \"TimeZoneId\" varchar(100) NOT NULL, \"ProjectionJson\" jsonb NOT NULL, " +
        "\"NarrativeStatus\" varchar(16) NOT NULL CHECK (\"NarrativeStatus\" IN ('Unavailable','Pending','Available','Failed')), " +
        "\"NarrativeText\" varchar(16384), \"NarrativeSourceIds\" jsonb NOT NULL DEFAULT '[]'::jsonb, \"SharedAdvisorUserId\" varchar(450), \"SharedByUserId\" varchar(450), " +
        "\"SharedAt\" timestamptz, \"CreatedAt\" timestamptz NOT NULL, \"Version\" integer NOT NULL CHECK (\"Version\" > 0), " +
        "UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"CropCycleId\") REFERENCES agro.crop_cycles (\"FarmId\", \"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"RequestedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"SharedAdvisorUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"SharedByUserId\") REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT); " +
        "CREATE INDEX \"IX_weekly_summaries_FarmId_WeekStartDate\" ON agro.weekly_summaries (\"FarmId\", \"WeekStartDate\"); " +
        "CREATE INDEX \"IX_weekly_summaries_SharedAdvisorUserId_FarmId_SharedAt\" ON agro.weekly_summaries (\"SharedAdvisorUserId\", \"FarmId\", \"SharedAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE IF EXISTS agro.weekly_summaries;");
}
