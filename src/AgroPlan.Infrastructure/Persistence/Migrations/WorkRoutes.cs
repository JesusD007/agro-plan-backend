using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000022_WorkRoutes")]
public sealed class WorkRoutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.work_routes (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, " +
        "\"RouteId\" uuid NOT NULL, " +
        "\"FarmId\" uuid NOT NULL, " +
        "\"WorkerUserId\" character varying(450) NOT NULL, " +
        "\"Version\" integer NOT NULL CHECK (\"Version\" > 0), " +
        "\"OrderedTaskIdsJson\" jsonb NOT NULL CHECK (jsonb_typeof(\"OrderedTaskIdsJson\") = 'array'), " +
        "\"TaskSourcesJson\" jsonb NOT NULL CHECK (jsonb_typeof(\"TaskSourcesJson\") = 'array'), " +
        "\"GeometryGeoJson\" jsonb CHECK (\"GeometryGeoJson\" IS NULL OR jsonb_typeof(\"GeometryGeoJson\") = 'object'), " +
        "\"Status\" character varying(16) NOT NULL CHECK (\"Status\" IN ('Proposed','Confirmed','Stale')), " +
        "\"CreatedAt\" timestamp with time zone NOT NULL, " +
        "\"ConfirmedByUserId\" character varying(450), " +
        "\"ConfirmedAt\" timestamp with time zone, " +
        "\"StaleAt\" timestamp with time zone, " +
        "UNIQUE (\"FarmId\", \"RouteId\", \"Version\"), " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, " +
        "FOREIGN KEY (\"FarmId\", \"WorkerUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"ConfirmedByUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT);" +
        "CREATE INDEX \"IX_work_routes_FarmId_WorkerUserId_RouteId_Version\" " +
        "ON agro.work_routes (\"FarmId\", \"WorkerUserId\", \"RouteId\", \"Version\" DESC);");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.work_routes;");
}
