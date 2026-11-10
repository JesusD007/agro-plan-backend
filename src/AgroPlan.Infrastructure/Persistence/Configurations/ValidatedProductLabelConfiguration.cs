using AgroPlan.Domain.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class ValidatedProductLabelConfiguration : IEntityTypeConfiguration<ValidatedProductLabel>
{
    public void Configure(EntityTypeBuilder<ValidatedProductLabel> builder)
    {
        builder.ToTable("validated_product_labels", "agro");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductRegistrationId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FormulationId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CropId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.IntendedUseId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Jurisdiction).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SourceVersion).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaximumResidueLimit).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.ProductRegistrationId, x.FormulationId, x.CropId, x.IntendedUseId, x.Jurisdiction });
    }
}
