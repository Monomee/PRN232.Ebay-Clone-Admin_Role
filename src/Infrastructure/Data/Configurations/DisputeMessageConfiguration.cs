using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbayClone.Infrastructure.Data.Configurations;

public class DisputeMessageConfiguration : IEntityTypeConfiguration<DisputeMessage>
{
    public void Configure(EntityTypeBuilder<DisputeMessage> builder)
    {
        builder.HasKey(dm => dm.Id);

        builder.Property(dm => dm.SenderType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(dm => dm.MessageType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(dm => dm.Content)
            .IsRequired();

        builder.Property(dm => dm.OfferAmount)
            .HasPrecision(18, 2);

        builder.HasOne(dm => dm.Dispute)
            .WithMany(d => d.Messages)
            .HasForeignKey(dm => dm.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dm => dm.Sender)
            .WithMany()
            .HasForeignKey(dm => dm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(dm => dm.DisputeId);
        builder.HasIndex(dm => dm.CreatedAt);
        builder.HasIndex(dm => new { dm.DisputeId, dm.CreatedAt });
    }
}
