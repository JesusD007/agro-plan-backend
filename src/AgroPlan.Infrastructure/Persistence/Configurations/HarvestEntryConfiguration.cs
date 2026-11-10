using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class HarvestEntryConfiguration : IEntityTypeConfiguration<HarvestEntry>
{
    public void Configure(EntityTypeBuilder<HarvestEntry> builder)
    {
        builder.ToTable("harvest_entries", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.AssessmentEvidenceJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CorrectionReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FarmId, x.HarvestedAt });
        builder.HasOne<Parcel>().WithMany().HasForeignKey(x => new { x.FarmId, x.ParcelId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany().HasForeignKey(x => new { x.FarmId, x.CropCycleId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HarvestEntry>().WithMany().HasForeignKey(x => new { x.FarmId, x.CorrectsEntryId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
