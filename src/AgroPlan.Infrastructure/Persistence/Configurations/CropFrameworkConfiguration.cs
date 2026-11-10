using AgroPlan.Domain.Crops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class CropFrameworkConfiguration : IEntityTypeConfiguration<CropFramework>
{
    public void Configure(EntityTypeBuilder<CropFramework> builder)
    {
        builder.ToTable("crop_frameworks", "agro", table =>
        {
            table.HasCheckConstraint("CK_crop_frameworks_spacing", "\"ValidationStatus\" <> 'Validated' OR (\"PlantSpacingMeters\" > 0 AND \"RowSpacingMeters\" > 0 AND \"AisleAllowanceRatio\" >= 0 AND \"AisleAllowanceRatio\" < 1 AND \"SourceName\" IS NOT NULL)");
            table.HasCheckConstraint("CK_crop_frameworks_validity", "\"ValidTo\" IS NULL OR \"ValidFrom\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.PlantSpacingMeters).HasPrecision(12, 4);
        builder.Property(item => item.RowSpacingMeters).HasPrecision(12, 4);
        builder.Property(item => item.AisleAllowanceRatio).HasPrecision(8, 6);
        builder.Property(item => item.SourceName).HasMaxLength(240);
        builder.Property(item => item.SourceUrl).HasMaxLength(2048);
        builder.Property(item => item.ValidationStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Region).HasMaxLength(160);
        builder.HasOne<CropCatalogEntry>().WithMany(item => item.Frameworks).HasForeignKey(item => item.CropCatalogEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.CropCatalogEntryId, item.ValidationStatus });
    }
}
