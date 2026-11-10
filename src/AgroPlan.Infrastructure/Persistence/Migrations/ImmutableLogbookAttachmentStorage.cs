using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000011_ImmutableLogbookAttachmentStorage")]
public sealed class ImmutableLogbookAttachmentStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.logbook_attachments ADD COLUMN \"UploadObjectKey\" varchar(512), ADD COLUMN \"FinalizingAt\" timestamptz; " +
        "UPDATE agro.logbook_attachments SET \"UploadObjectKey\" = \"ObjectKey\" WHERE \"UploadObjectKey\" IS NULL; " +
        "ALTER TABLE agro.logbook_attachments ALTER COLUMN \"UploadObjectKey\" SET NOT NULL; " +
        "CREATE UNIQUE INDEX \"IX_logbook_attachments_UploadObjectKey\" ON agro.logbook_attachments (\"UploadObjectKey\");");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP INDEX IF EXISTS agro.\"IX_logbook_attachments_UploadObjectKey\"; " +
        "ALTER TABLE agro.logbook_attachments DROP COLUMN IF EXISTS \"UploadObjectKey\", DROP COLUMN IF EXISTS \"FinalizingAt\";");
}
