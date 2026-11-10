using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000007_LogbookAttachments")]
public sealed class LogbookAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "CREATE TABLE agro.logbook_attachments (" +
        "\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"LogbookEntryId\" uuid NOT NULL, \"ObjectKey\" varchar(512) NOT NULL, " +
        "\"ContentType\" varchar(64) NOT NULL, \"ExpectedSizeBytes\" bigint NOT NULL, \"SizeBytes\" bigint, \"UploadedByUserId\" varchar(450) NOT NULL, " +
        "\"CreatedAt\" timestamptz NOT NULL, \"ExpiresAt\" timestamptz NOT NULL, \"FinalizedAt\" timestamptz, UNIQUE (\"FarmId\", \"Id\"), " +
        "FOREIGN KEY (\"FarmId\", \"LogbookEntryId\") REFERENCES agro.logbook_entries (\"FarmId\", \"Id\") ON DELETE RESTRICT); " +
        "CREATE UNIQUE INDEX \"IX_logbook_attachments_ObjectKey\" ON agro.logbook_attachments (\"ObjectKey\"); " +
        "CREATE INDEX \"IX_logbook_attachments_FarmId_LogbookEntryId\" ON agro.logbook_attachments (\"FarmId\", \"LogbookEntryId\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE IF EXISTS agro.logbook_attachments;");
}
