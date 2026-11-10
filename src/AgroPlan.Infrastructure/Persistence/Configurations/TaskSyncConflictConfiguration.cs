using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class TaskSyncConflictConfiguration : IEntityTypeConfiguration<TaskSyncConflict>
{
    public void Configure(EntityTypeBuilder<TaskSyncConflict> builder)
    {
        builder.ToTable("task_sync_conflicts", "agro");
        builder.HasKey(conflict => conflict.Id);
        builder.HasAlternateKey(conflict => new { conflict.FarmId, conflict.WorkTaskId, conflict.Id });
        builder.HasAlternateKey(conflict => new { conflict.FarmId, conflict.Id });
        builder.Property(conflict => conflict.WorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(conflict => conflict.ClientValuesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(conflict => conflict.ServerValuesJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(conflict => new { conflict.FarmId, conflict.WorkerUserId, conflict.ClientOperationId }).IsUnique();
        builder.HasIndex(conflict => new { conflict.FarmId, conflict.WorkTaskId, conflict.DetectedAt });
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(conflict => new { conflict.FarmId, conflict.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(conflict => new { conflict.FarmId, UserId = conflict.WorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
