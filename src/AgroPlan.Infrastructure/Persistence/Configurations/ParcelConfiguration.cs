using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> builder)
    {
        builder.ToTable("parcels", "agro");
        builder.HasKey(parcel => parcel.Id);
        builder.HasAlternateKey(parcel => new { parcel.FarmId, parcel.Id });
        builder.Property(parcel => parcel.Name).HasMaxLength(160).IsRequired();
        builder.Property(parcel => parcel.Location).HasMaxLength(240);
        builder.Property(parcel => parcel.AreaHectares).HasPrecision(14, 4).IsRequired();
        builder.Property(parcel => parcel.CreatedAt).IsRequired();
        builder.Property(parcel => parcel.UpdatedAt).IsRequired();
        builder.HasIndex(parcel => new { parcel.FarmId, parcel.Name }).IsUnique();
    }
}
