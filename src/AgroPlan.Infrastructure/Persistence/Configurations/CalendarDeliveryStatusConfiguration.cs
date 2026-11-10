using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using AgroPlan.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class CalendarDeliveryStatusConfiguration : IEntityTypeConfiguration<CalendarDeliveryStatus>
{
    public void Configure(EntityTypeBuilder<CalendarDeliveryStatus> builder)
    {
        builder.ToTable("calendar_delivery_statuses", "agro");
        builder.HasKey(status => status.Id);
        builder.Property(status => status.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(status => status.LastErrorCode).HasMaxLength(80);
        builder.HasIndex(status => status.OutboxEventId).IsUnique();
        builder.HasIndex(status => new { status.FarmId, status.WorkTaskId, status.CreatedAt });
        builder.HasOne<OutboxMessage>().WithMany().HasForeignKey(status => status.OutboxEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(status => new { status.FarmId, status.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Farm>().WithMany().HasForeignKey(status => status.FarmId).OnDelete(DeleteBehavior.Cascade);
    }
}
