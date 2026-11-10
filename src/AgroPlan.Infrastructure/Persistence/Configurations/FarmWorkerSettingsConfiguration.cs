using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class FarmWorkerSettingsConfiguration : IEntityTypeConfiguration<FarmWorkerSettings>
{
    public void Configure(EntityTypeBuilder<FarmWorkerSettings> builder)
    {
        builder.ToTable("farm_worker_settings", "agro", table =>
        {
            table.HasCheckConstraint("CK_farm_worker_settings_BadgeThreshold", "\"BadgeThresholdPoints\" > 0");
            table.HasCheckConstraint("CK_farm_worker_settings_TeamGoalTarget", "\"TeamGoalTargetPoints\" > 0");
            table.HasCheckConstraint("CK_farm_worker_settings_RuleVersion", "\"RuleVersion\" > 0");
        });
        builder.HasKey(settings => settings.FarmId);
        builder.Property(settings => settings.TimeZoneId).HasMaxLength(100);
        builder.HasOne<Farm>().WithOne().HasForeignKey<FarmWorkerSettings>(settings => settings.FarmId).OnDelete(DeleteBehavior.Cascade);
    }
}
