using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class RecoveryAuditEntryConfiguration : IEntityTypeConfiguration<RecoveryAuditEntry>
{
    public void Configure(EntityTypeBuilder<RecoveryAuditEntry> builder)
    {
        builder.ToTable("recovery_audit", "identity");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.SupportUserId).HasMaxLength(450).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.Note).HasMaxLength(500);
        builder.HasIndex(entry => new { entry.RecoveryRequestId, entry.CreatedAt });
        builder.HasOne<RecoveryRequest>().WithMany().HasForeignKey(entry => entry.RecoveryRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(entry => entry.SupportUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
