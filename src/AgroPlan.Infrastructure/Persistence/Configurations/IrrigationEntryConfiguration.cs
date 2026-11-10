using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class IrrigationEntryConfiguration : IEntityTypeConfiguration<IrrigationEntry>
{
    public void Configure(EntityTypeBuilder<IrrigationEntry> builder)
    {
        builder.ToTable("irrigation_entries", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.WaterSource).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CorrectionReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FarmId, x.OccurredAt });
        builder.HasOne<Parcel>().WithMany().HasForeignKey(x => new { x.FarmId, x.ParcelId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany().HasForeignKey(x => new { x.FarmId, x.CropCycleId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IrrigationEntry>().WithMany().HasForeignKey(x => new { x.FarmId, x.CorrectsEntryId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
