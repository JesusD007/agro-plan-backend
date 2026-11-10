using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class VerifiedWageEntryConfiguration : IEntityTypeConfiguration<VerifiedWageEntry>
{
    public void Configure(EntityTypeBuilder<VerifiedWageEntry> builder)
    {
        builder.ToTable("verified_wage_entries", "agro");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.WorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(entry => entry.Amount).HasPrecision(14, 2).IsRequired();
        builder.Property(entry => entry.Unit).HasMaxLength(64).IsRequired();
        builder.HasIndex(entry => new { entry.FarmId, entry.WorkTaskId }).IsUnique();
        builder.HasIndex(entry => new { entry.FarmId, entry.WorkerUserId, entry.VerifiedAt });
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(entry => new { entry.FarmId, entry.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(entry => new { entry.FarmId, UserId = entry.WorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
