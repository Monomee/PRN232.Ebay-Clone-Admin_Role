using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbayClone.Infrastructure.Data.Configurations;

public class PlatformFeeConfiguration : IEntityTypeConfiguration<PlatformFee>
{
    public void Configure(EntityTypeBuilder<PlatformFee> builder)
    {
        builder.ToTable("PlatformFees");

        builder.HasKey(x => x.Id);

        // Configure decimal properties with precision and scale
        builder.Property(x => x.Percentage)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.FixedAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MinAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MaxAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.FeeType)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Foreign key to Category (optional)
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
