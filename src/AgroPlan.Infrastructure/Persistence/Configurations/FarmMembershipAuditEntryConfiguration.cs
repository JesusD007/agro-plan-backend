using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class FarmMembershipAuditEntryConfiguration : IEntityTypeConfiguration<FarmMembershipAuditEntry>
{
    public void Configure(EntityTypeBuilder<FarmMembershipAuditEntry> builder)
    {
        builder.ToTable("farm_membership_audit", "agro");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.TargetUserId).HasMaxLength(450).IsRequired();
        builder.Property(entry => entry.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.HasIndex(entry => new { entry.FarmId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.TargetUserId, entry.OccurredAt });
    }
}
