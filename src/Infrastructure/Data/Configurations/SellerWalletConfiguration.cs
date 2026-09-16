using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbayClone.Infrastructure.Data.Configurations;

public class SellerWalletConfiguration : IEntityTypeConfiguration<SellerWallet>
{
    public void Configure(EntityTypeBuilder<SellerWallet> builder)
    {
        builder.ToTable("SellerWallets");

        builder.HasKey(x => x.Id);

        // Configure decimal properties with precision and scale
        builder.Property(x => x.PendingBalance)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.AvailableBalance)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.TotalEarnings)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.TotalWithdrawn)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.DisputedBalance)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.TotalRefunded)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.LockedBalance)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // Foreign key to User (Seller)
        builder.HasOne(x => x.Seller)
            .WithMany()
            .HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
