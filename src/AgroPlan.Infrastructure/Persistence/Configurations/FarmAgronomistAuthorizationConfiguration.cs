using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class FarmAgronomistAuthorizationConfiguration : IEntityTypeConfiguration<FarmAgronomistAuthorization>
{
    public void Configure(EntityTypeBuilder<FarmAgronomistAuthorization> builder)
    {
        builder.ToTable("farm_agronomist_authorizations", "agro");
        builder.HasKey(x => new { x.FarmId, x.AgronomistUserId });
        builder.Property(x => x.AgronomistUserId).HasMaxLength(450);
        builder.Property(x => x.GrantedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RevokedByUserId).HasMaxLength(450);
        builder.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AgronomistUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
