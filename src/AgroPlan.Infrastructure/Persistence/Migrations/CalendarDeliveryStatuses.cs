using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000024_CalendarDeliveryStatuses")]
public sealed class CalendarDeliveryStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.calendar_delivery_statuses (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, " +
        "\"OutboxEventId\" uuid NOT NULL UNIQUE, " +
        "\"FarmId\" uuid NOT NULL, " +
        "\"WorkTaskId\" uuid NOT NULL, " +
        "\"RevisionId\" uuid NOT NULL, " +
        "\"TaskVersion\" integer NOT NULL CHECK (\"TaskVersion\" > 0), " +
        "\"State\" character varying(16) NOT NULL CHECK (\"State\" IN ('Pending','Succeeded','Retryable','Terminal')), " +
        "\"AttemptCount\" integer NOT NULL CHECK (\"AttemptCount\" >= 0), " +
        "\"LastErrorCode\" character varying(80), " +
        "\"LastAttemptAt\" timestamp with time zone, " +
        "\"NextAttemptAt\" timestamp with time zone, " +
        "\"DeliveredAt\" timestamp with time zone, " +
        "\"CreatedAt\" timestamp with time zone NOT NULL, " +
        "FOREIGN KEY (\"OutboxEventId\") REFERENCES infra.outbox_messages (\"Id\") ON DELETE RESTRICT, " +
        "FOREIGN KEY (\"FarmId\", \"WorkTaskId\") REFERENCES agro.work_tasks (\"FarmId\", \"Id\") ON DELETE CASCADE, " +
        "FOREIGN KEY (\"FarmId\") REFERENCES agro.farms (\"Id\") ON DELETE CASCADE);" +
        "CREATE INDEX \"IX_calendar_delivery_statuses_FarmId_WorkTaskId_CreatedAt\" " +
        "ON agro.calendar_delivery_statuses (\"FarmId\", \"WorkTaskId\", \"CreatedAt\");");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE IF EXISTS agro.calendar_delivery_statuses;");
}
