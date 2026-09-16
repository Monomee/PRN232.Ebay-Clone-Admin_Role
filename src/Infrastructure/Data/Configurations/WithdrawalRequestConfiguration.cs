using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbayClone.Infrastructure.Data.Configurations;

public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
    {
        builder.ToTable("WithdrawalRequests");

        builder.HasKey(x => x.Id);

        // Configure decimal property with precision and scale
        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.BankName)
            .HasMaxLength(100);

        builder.Property(x => x.BankAccountNumber)
            .HasMaxLength(50);

        builder.Property(x => x.BankAccountName)
            .HasMaxLength(100);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.TransactionId)
            .HasMaxLength(100);

        builder.Property(x => x.RequestedAt)
            .IsRequired();

        // Foreign keys
        builder.HasOne(x => x.Seller)
            .WithMany()
            .HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Processor)
            .WithMany()
            .HasForeignKey(x => x.ProcessedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
