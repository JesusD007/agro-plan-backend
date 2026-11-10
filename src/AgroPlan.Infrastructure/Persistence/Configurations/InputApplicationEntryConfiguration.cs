using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class InputApplicationEntryConfiguration : IEntityTypeConfiguration<InputApplicationEntry>, IEntityTypeConfiguration<ValidatedApplicationFrequencyRule>
{
    public void Configure(EntityTypeBuilder<InputApplicationEntry> builder)
    {
        builder.ToTable("input_application_entries", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.ProductRegistrationId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FormulationId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CropId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.IntendedUseId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CorrectionReason).HasMaxLength(1000);
        builder.Property(x => x.CitedLabelSource).HasMaxLength(500);
        builder.Property(x => x.CitedLabelVersion).HasMaxLength(200);
        builder.HasIndex(x => new { x.FarmId, x.AppliedAt });
        builder.HasIndex(x => new { x.FarmId, x.ProductRegistrationId, x.FormulationId, x.CropId, x.IntendedUseId, x.AppliedAt });
        builder.HasOne<Parcel>().WithMany().HasForeignKey(x => new { x.FarmId, x.ParcelId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany().HasForeignKey(x => new { x.FarmId, x.CropCycleId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InputApplicationEntry>().WithMany().HasForeignKey(x => new { x.FarmId, x.CorrectsEntryId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ValidatedApplicationFrequencyRule> builder)
    {
        builder.ToTable("validated_application_frequency_rules", "agro");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductRegistrationId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FormulationId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CropId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.IntendedUseId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Jurisdiction).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SourceVersion).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.ProductRegistrationId, x.FormulationId, x.CropId, x.IntendedUseId, x.Jurisdiction });
    }
}
