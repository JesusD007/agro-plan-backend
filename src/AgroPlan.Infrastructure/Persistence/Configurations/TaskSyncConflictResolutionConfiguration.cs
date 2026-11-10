using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class TaskSyncConflictResolutionConfiguration : IEntityTypeConfiguration<TaskSyncConflictResolution>
{
    public void Configure(EntityTypeBuilder<TaskSyncConflictResolution> builder)
    {
        builder.ToTable("task_sync_conflict_resolutions", "agro");
        builder.HasKey(resolution => resolution.Id);
        builder.HasIndex(resolution => resolution.ConflictId).IsUnique();
        builder.Property(resolution => resolution.DecidedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(resolution => resolution.Resolution).HasMaxLength(16).IsRequired();
        builder.Property(resolution => resolution.SelectedValuesJson).HasColumnType("jsonb").IsRequired();
        builder.HasOne<TaskSyncConflict>().WithMany()
            .HasForeignKey(resolution => new { resolution.FarmId, resolution.ConflictId })
            .HasPrincipalKey(conflict => new { conflict.FarmId, conflict.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(resolution => new { resolution.FarmId, UserId = resolution.DecidedByUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
