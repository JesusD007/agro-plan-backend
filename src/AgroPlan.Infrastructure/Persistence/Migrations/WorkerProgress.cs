using AgroPlan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgroPlan.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgroPlanDbContext))]
[Migration("20261110000021_WorkerProgress")]
public sealed class WorkerProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "ALTER TABLE agro.work_tasks ADD COLUMN \"PointsForVerification\" integer NOT NULL DEFAULT 10 CHECK (\"PointsForVerification\" BETWEEN 0 AND 10000);\n" +
        "ALTER TABLE agro.work_tasks ADD COLUMN \"CompleteEvidenceBonusPoints\" integer NOT NULL DEFAULT 0 CHECK (\"CompleteEvidenceBonusPoints\" BETWEEN 0 AND 10000);\n" +
        "CREATE TABLE agro.farm_worker_settings (\"FarmId\" uuid NOT NULL PRIMARY KEY REFERENCES agro.farms (\"Id\") ON DELETE CASCADE, \"RankingsEnabled\" boolean NOT NULL DEFAULT false, \"TimeZoneId\" character varying(100), \"BadgeThresholdPoints\" integer NOT NULL DEFAULT 100 CHECK (\"BadgeThresholdPoints\" > 0), \"TeamGoalTargetPoints\" integer NOT NULL DEFAULT 1000 CHECK (\"TeamGoalTargetPoints\" > 0), \"RuleVersion\" integer NOT NULL DEFAULT 1 CHECK (\"RuleVersion\" > 0));\n" +
        "CREATE TABLE agro.verified_work_rewards (\"Id\" uuid NOT NULL PRIMARY KEY, \"FarmId\" uuid NOT NULL, \"WorkerUserId\" character varying(450) NOT NULL, \"WorkTaskId\" uuid, \"Kind\" character varying(16) NOT NULL CHECK (\"Kind\" IN ('Points','Badge','Manual')), \"RuleKey\" character varying(100) NOT NULL, \"Points\" integer NOT NULL CHECK (\"Points\" >= 0), \"RuleVersion\" integer NOT NULL CHECK (\"RuleVersion\" > 0), \"AwardedAt\" timestamp with time zone NOT NULL, \"GrantedByUserId\" character varying(450), \"Description\" character varying(1000), UNIQUE (\"FarmId\", \"Id\"), FOREIGN KEY (\"FarmId\", \"WorkerUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT, FOREIGN KEY (\"FarmId\", \"WorkTaskId\") REFERENCES agro.work_tasks (\"FarmId\", \"Id\") ON DELETE RESTRICT, FOREIGN KEY (\"FarmId\", \"GrantedByUserId\") REFERENCES agro.farm_memberships (\"FarmId\", \"UserId\") ON DELETE RESTRICT);\n" +
        "CREATE UNIQUE INDEX \"IX_verified_work_rewards_FarmId_WorkTaskId_Kind_RuleKey\" ON agro.verified_work_rewards (\"FarmId\", \"WorkTaskId\", \"Kind\", \"RuleKey\") WHERE \"WorkTaskId\" IS NOT NULL;\n" +
        "CREATE INDEX \"IX_verified_work_rewards_FarmId_WorkerUserId_AwardedAt\" ON agro.verified_work_rewards (\"FarmId\", \"WorkerUserId\", \"AwardedAt\");\n" +
        "CREATE UNIQUE INDEX \"IX_verified_work_rewards_FarmId_WorkerUserId_Kind_RuleKey_Badge\" ON agro.verified_work_rewards (\"FarmId\", \"WorkerUserId\", \"Kind\", \"RuleKey\") WHERE \"Kind\" = 'Badge';");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE IF EXISTS agro.verified_work_rewards;\n" +
        "DROP TABLE IF EXISTS agro.farm_worker_settings;\n" +
        "ALTER TABLE agro.work_tasks DROP COLUMN IF EXISTS \"CompleteEvidenceBonusPoints\";\n" +
        "ALTER TABLE agro.work_tasks DROP COLUMN IF EXISTS \"PointsForVerification\";");
}
