using AgroPlan.Domain.Crops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class CropCatalogConfiguration : IEntityTypeConfiguration<CropCatalogEntry>
{
    public void Configure(EntityTypeBuilder<CropCatalogEntry> builder)
    {
        builder.ToTable("crop_catalog", "agro");
        builder.HasKey(crop => crop.Id);
        builder.Property(crop => crop.Code).HasMaxLength(40).IsRequired();
        builder.HasIndex(crop => crop.Code).IsUnique();
        builder.Property(crop => crop.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(crop => crop.SourceName).HasMaxLength(240).IsRequired();
        builder.Property(crop => crop.ValidationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(crop => crop.Region).HasMaxLength(160);
        builder.HasMany(crop => crop.Parameters)
            .WithOne(parameter => parameter.CropCatalogEntry)
            .HasForeignKey(parameter => parameter.CropCatalogEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AgronomicParameterConfiguration : IEntityTypeConfiguration<AgronomicParameter>
{
    public void Configure(EntityTypeBuilder<AgronomicParameter> builder)
    {
        builder.ToTable("agronomic_parameters", "agro", table =>
        {
            table.HasCheckConstraint(
                "CK_agronomic_parameters_validated_metadata",
                "\"ValidationStatus\" <> 'Validated' OR (\"NumericValue\" IS NOT NULL AND \"Unit\" IS NOT NULL AND \"SourceName\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_agronomic_parameters_validity_range",
                "\"ValidTo\" IS NULL OR \"ValidFrom\" IS NULL OR \"ValidTo\" >= \"ValidFrom\"");
        });
        builder.HasKey(parameter => parameter.Id);
        builder.Property(parameter => parameter.Key).HasMaxLength(80).IsRequired();
        builder.Property(parameter => parameter.NumericValue).HasPrecision(20, 6);
        builder.Property(parameter => parameter.Unit).HasMaxLength(48);
        builder.Property(parameter => parameter.ValidationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(parameter => parameter.SourceName).HasMaxLength(240);
        builder.Property(parameter => parameter.SourceUrl).HasMaxLength(2048);
        builder.Property(parameter => parameter.Region).HasMaxLength(160);
        builder.HasIndex(parameter => new { parameter.CropCatalogEntryId, parameter.Key });
    }
}
