using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.ToTable("farms", "agro");
        builder.HasKey(farm => farm.Id);
        builder.Property(farm => farm.Name).HasMaxLength(160).IsRequired();
        builder.Property(farm => farm.Location).HasMaxLength(240);
        builder.Property(farm => farm.CreatedAt).IsRequired();
        builder.Property(farm => farm.WorkerEntitlement).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.HasIndex(farm => farm.CreatedAt);

        builder.HasMany(farm => farm.Memberships)
            .WithOne(membership => membership.Farm)
            .HasForeignKey(membership => membership.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(farm => farm.Parcels)
            .WithOne(parcel => parcel.Farm)
            .HasForeignKey(parcel => parcel.FarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class FarmMembershipConfiguration : IEntityTypeConfiguration<FarmMembership>
{
    public void Configure(EntityTypeBuilder<FarmMembership> builder)
    {
        builder.ToTable("farm_memberships", "agro");
        builder.HasKey(membership => new { membership.FarmId, membership.UserId });
        builder.Property(membership => membership.UserId).HasMaxLength(450).IsRequired();
        builder.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(membership => membership.IsActive).IsRequired();
        builder.Property(membership => membership.CreatedAt).IsRequired();
        builder.HasIndex(membership => new { membership.UserId, membership.IsActive });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
