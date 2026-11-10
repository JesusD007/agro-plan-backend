using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class CropCycleConfiguration : IEntityTypeConfiguration<CropCycle>
{
    public void Configure(EntityTypeBuilder<CropCycle> builder)
    {
        builder.ToTable("crop_cycles", "agro");
        builder.HasKey(cycle => cycle.Id);
        builder.HasAlternateKey(cycle => new { cycle.FarmId, cycle.Id });
        builder.Property(cycle => cycle.DesiredPlantingDate).HasColumnType("date");
        builder.Property(cycle => cycle.Variety).HasMaxLength(120);
        builder.Property(cycle => cycle.Notes).HasMaxLength(2000);
        builder.Property(cycle => cycle.CreatedAt).IsRequired();
        builder.Property(cycle => cycle.UpdatedAt).IsRequired();
        builder.HasIndex(cycle => new { cycle.FarmId, cycle.ParcelId });
        builder.HasIndex(cycle => new { cycle.FarmId, cycle.DesiredPlantingDate });
        builder.HasOne<Parcel>()
            .WithMany()
            .HasForeignKey(cycle => new { cycle.FarmId, cycle.ParcelId })
            .HasPrincipalKey(parcel => new { parcel.FarmId, parcel.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(cycle => cycle.CropCatalogEntry)
            .WithMany()
            .HasForeignKey(cycle => cycle.CropCatalogEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropFramework>().WithMany().HasForeignKey(cycle => cycle.SelectedFrameworkId).OnDelete(DeleteBehavior.Restrict);
    }
}
