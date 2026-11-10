using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class ExpenseEntryConfiguration : IEntityTypeConfiguration<ExpenseEntry>
{
    public void Configure(EntityTypeBuilder<ExpenseEntry> builder)
    {
        builder.ToTable("expense_entries", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Unit).HasMaxLength(64);
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CorrectionReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.FarmId, x.OccurredAt });
        builder.HasOne<Parcel>().WithMany().HasForeignKey(x => new { x.FarmId, x.ParcelId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany().HasForeignKey(x => new { x.FarmId, x.CropCycleId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExpenseEntry>().WithMany().HasForeignKey(x => new { x.FarmId, x.CorrectsEntryId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
