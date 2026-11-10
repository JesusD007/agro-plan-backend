using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class TaskScheduleRevisionConfiguration : IEntityTypeConfiguration<TaskScheduleRevision>
{
    public void Configure(EntityTypeBuilder<TaskScheduleRevision> builder)
    {
        builder.ToTable("task_schedule_revisions", "agro");
        builder.HasKey(revision => revision.Id);
        builder.HasAlternateKey(revision => new { revision.FarmId, revision.WorkTaskId, revision.Id });
        builder.Property(revision => revision.PreviousDate).HasColumnType("date").IsRequired();
        builder.Property(revision => revision.ProposedDate).HasColumnType("date").IsRequired();
        builder.Property(revision => revision.Explanation).HasMaxLength(1000).IsRequired();
        builder.Property(revision => revision.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(revision => revision.ConfirmedByUserId).HasMaxLength(450);
        builder.Property(revision => revision.NotifiedAt);
        builder.HasIndex(revision => new { revision.FarmId, revision.SourceEventId, revision.WorkTaskId }).IsUnique();
        builder.HasIndex(revision => new { revision.FarmId, revision.WorkTaskId, revision.ProposedAt });
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(revision => new { revision.FarmId, revision.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Farm>().WithMany().HasForeignKey(revision => revision.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OutboxMessage>().WithMany().HasForeignKey(revision => revision.SourceEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(revision => new { revision.FarmId, UserId = revision.ConfirmedByUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
