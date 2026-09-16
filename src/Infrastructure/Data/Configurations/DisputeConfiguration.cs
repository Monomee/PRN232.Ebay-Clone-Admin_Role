using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbayClone.Infrastructure.Data.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        // Configure decimal properties with precision and scale
        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.LastOfferAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.RefundAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
