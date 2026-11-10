using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000001_AccountSessionsAndRecovery")]
public sealed class AccountSessionsAndRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "CREATE TABLE identity.refresh_sessions (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"FamilyId\" uuid NOT NULL, " +
            "\"UserId\" character varying(450) NOT NULL REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE CASCADE, " +
            "\"TokenHash\" character(64) NOT NULL UNIQUE, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"ExpiresAt\" timestamp with time zone NOT NULL, " +
            "\"ConsumedAt\" timestamp with time zone, " +
            "\"RevokedAt\" timestamp with time zone, " +
            "\"ReplacedBySessionId\" uuid REFERENCES identity.refresh_sessions (\"Id\") ON DELETE SET NULL);\n" +
            "CREATE INDEX \"IX_refresh_sessions_FamilyId_RevokedAt\" ON identity.refresh_sessions (\"FamilyId\", \"RevokedAt\");\n" +
            "CREATE INDEX \"IX_refresh_sessions_UserId_ExpiresAt\" ON identity.refresh_sessions (\"UserId\", \"ExpiresAt\");\n" +
            "CREATE TABLE identity.recovery_requests (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"UserId\" character varying(450) REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE SET NULL, " +
            "\"EmailHash\" character(64) NOT NULL, " +
            "\"SourceIpHash\" character(64) NOT NULL, " +
            "\"RecoveryCodeHash\" character(64) NOT NULL, " +
            "\"CreatedAt\" timestamp with time zone NOT NULL, " +
            "\"CodeExpiresAt\" timestamp with time zone NOT NULL, " +
            "\"CodeAttempts\" integer NOT NULL, " +
            "\"EmailVerifiedAt\" timestamp with time zone, " +
            "\"Status\" character varying(32) NOT NULL, " +
            "\"ReviewedAt\" timestamp with time zone, " +
            "\"ReviewedByUserId\" character varying(450) REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE SET NULL, " +
            "\"ReviewNote\" character varying(500), " +
            "\"ResetTokenHash\" character(64), " +
            "\"ResetTokenExpiresAt\" timestamp with time zone, " +
            "\"ResetTokenConsumedAt\" timestamp with time zone);\n" +
            "CREATE INDEX \"IX_recovery_requests_EmailHash_CreatedAt\" ON identity.recovery_requests (\"EmailHash\", \"CreatedAt\");\n" +
            "CREATE INDEX \"IX_recovery_requests_SourceIpHash_CreatedAt\" ON identity.recovery_requests (\"SourceIpHash\", \"CreatedAt\");\n" +
            "CREATE INDEX \"IX_recovery_requests_UserId_Status_CreatedAt\" ON identity.recovery_requests (\"UserId\", \"Status\", \"CreatedAt\");\n" +
            "CREATE UNIQUE INDEX \"IX_recovery_requests_ResetTokenHash\" ON identity.recovery_requests (\"ResetTokenHash\");\n" +
            "CREATE TABLE identity.recovery_audit (" +
            "\"Id\" uuid NOT NULL PRIMARY KEY, " +
            "\"RecoveryRequestId\" uuid NOT NULL REFERENCES identity.recovery_requests (\"Id\") ON DELETE CASCADE, " +
            "\"SupportUserId\" character varying(450) NOT NULL REFERENCES identity.\"AspNetUsers\" (\"Id\") ON DELETE RESTRICT, " +
            "\"Action\" character varying(32) NOT NULL, " +
            "\"Note\" character varying(500), " +
            "\"CreatedAt\" timestamp with time zone NOT NULL);\n" +
            "CREATE INDEX \"IX_recovery_audit_RecoveryRequestId_CreatedAt\" ON identity.recovery_audit (\"RecoveryRequestId\", \"CreatedAt\");\n" +
            "INSERT INTO identity.\"AspNetRoles\" (\"Id\", \"Name\", \"NormalizedName\", \"ConcurrencyStamp\") " +
            "SELECT '00000000-0000-0000-0000-000000000010', 'Support', 'SUPPORT', '00000000-0000-0000-0000-000000000011' " +
            "WHERE NOT EXISTS (SELECT 1 FROM identity.\"AspNetRoles\" WHERE \"NormalizedName\" = 'SUPPORT');");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DROP TABLE IF EXISTS identity.recovery_audit;\n" +
            "DROP TABLE IF EXISTS identity.recovery_requests;\n" +
            "DROP TABLE IF EXISTS identity.refresh_sessions;\n" +
            "DELETE FROM identity.\"AspNetRoles\" r WHERE r.\"NormalizedName\" = 'SUPPORT' " +
            "AND NOT EXISTS (SELECT 1 FROM identity.\"AspNetUserRoles\" ur WHERE ur.\"RoleId\" = r.\"Id\");");
    }
}
