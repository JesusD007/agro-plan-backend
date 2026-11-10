using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("refresh_sessions", "identity");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.UserId).HasMaxLength(450).IsRequired();
        builder.Property(session => session.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => new { session.FamilyId, session.RevokedAt });
        builder.HasIndex(session => new { session.UserId, session.ExpiresAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RefreshSession>().WithMany().HasForeignKey(session => session.ReplacedBySessionId).OnDelete(DeleteBehavior.SetNull);
    }
}
