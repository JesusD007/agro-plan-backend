using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class TrustedAgronomistVerificationConfiguration : IEntityTypeConfiguration<TrustedAgronomistVerification>
{
    public void Configure(EntityTypeBuilder<TrustedAgronomistVerification> builder)
    {
        builder.ToTable("trusted_agronomist_verifications", "agro");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.VerifiedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CredentialReference).HasMaxLength(300).IsRequired();
        builder.Property(x => x.RevokedByUserId).HasMaxLength(450);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
