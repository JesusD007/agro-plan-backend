using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("work_tasks", "agro");
        builder.HasKey(task => task.Id);
        builder.HasAlternateKey(task => new { task.FarmId, task.Id });
        builder.Property(task => task.AssignedWorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(task => task.DueDate).HasColumnType("date").IsRequired();
        builder.Property(task => task.Description).HasMaxLength(1000).IsRequired();
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(task => task.WageAmount).HasPrecision(14, 2);
        builder.Property(task => task.WageUnit).HasMaxLength(64);
        builder.Property(task => task.OperationKind).HasMaxLength(64);
        builder.Property(task => task.CreatedAt).IsRequired();
        builder.HasIndex(task => new { task.FarmId, task.AssignedWorkerUserId, task.Status, task.DueDate });
        builder.HasOne<Farm>().WithMany().HasForeignKey(task => task.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(task => new { task.FarmId, UserId = task.AssignedWorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Parcel>().WithMany()
            .HasForeignKey(task => new { task.FarmId, task.ParcelId })
            .HasPrincipalKey(parcel => new { parcel.FarmId, parcel.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany()
            .HasForeignKey(task => new { task.FarmId, task.CropCycleId })
            .HasPrincipalKey(cycle => new { cycle.FarmId, cycle.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
