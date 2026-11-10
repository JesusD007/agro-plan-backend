using AgroPlan.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", "infra");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.EventType).HasMaxLength(120).IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(80);
        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.AvailableAt).IsRequired();
        builder.Property(message => message.AttemptCount).IsRequired();
        builder.HasIndex(message => new { message.AvailableAt, message.OccurredAt })
            .HasFilter("\"PublishedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
    }
}
