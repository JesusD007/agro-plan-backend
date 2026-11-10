using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class RecoveryRequestConfiguration : IEntityTypeConfiguration<RecoveryRequest>
{
    public void Configure(EntityTypeBuilder<RecoveryRequest> builder)
    {
        builder.ToTable("recovery_requests", "identity");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.EmailHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(request => request.SourceIpHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(request => request.RecoveryCodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(request => request.ResetTokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(request => request.Status).HasMaxLength(32).IsRequired();
        builder.Property(request => request.ReviewNote).HasMaxLength(500);
        builder.HasIndex(request => new { request.EmailHash, request.CreatedAt });
        builder.HasIndex(request => new { request.SourceIpHash, request.CreatedAt });
        builder.HasIndex(request => new { request.UserId, request.Status, request.CreatedAt });
        builder.HasIndex(request => request.ResetTokenHash).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(request => request.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(request => request.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
