using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class SyncOperationReceiptConfiguration : IEntityTypeConfiguration<SyncOperationReceipt>
{
    public void Configure(EntityTypeBuilder<SyncOperationReceipt> builder)
    {
        builder.ToTable("sync_operation_receipts", "agro");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.WorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(receipt => receipt.PayloadFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.Status).HasMaxLength(24).IsRequired();
        builder.Property(receipt => receipt.ResultJson).HasColumnType("jsonb").IsRequired();
        builder.Property(receipt => receipt.RecordedAt).IsRequired();
        builder.HasIndex(receipt => new { receipt.FarmId, receipt.WorkerUserId, receipt.ClientOperationId }).IsUnique();
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(receipt => new { receipt.FarmId, UserId = receipt.WorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
