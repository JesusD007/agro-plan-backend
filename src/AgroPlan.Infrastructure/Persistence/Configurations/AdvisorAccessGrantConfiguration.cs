using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class AdvisorAccessGrantConfiguration : IEntityTypeConfiguration<AdvisorAccessGrant>
{
    public void Configure(EntityTypeBuilder<AdvisorAccessGrant> builder)
    {
        builder.ToTable("advisor_access_grants", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.AdvisorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.GrantedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RevokedByUserId).HasMaxLength(450);
        builder.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Scopes).HasColumnType("integer[]").IsRequired();
        builder.HasIndex(x => new { x.FarmId, x.AdvisorUserId, x.ExpiresAt });
        builder.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
    }
}
