using System;

namespace EbayClone.Domain.Entities;

public partial class SellerWallet
{
    public int Id { get; set; }

    public int SellerId { get; set; }

    public decimal PendingBalance { get; set; } = 0; // Money on hold (new sales, awaiting clearance)

    public decimal AvailableBalance { get;  set; } = 0; // Money available for withdrawal
    public decimal LockedBalance { get; set; } = 0; // Money frozen during withdrawal processing


    public decimal TotalEarnings { get; private set; } = 0;

    public decimal TotalWithdrawn { get; private set; } = 0;
    public decimal DisputedBalance { get; set; } = 0; // Money frozen due to disputes

    public decimal TotalRefunded { get; set; } = 0; // Total refunded due to disputes/returns

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User? Seller { get; set; }

    public void CreditPending(decimal amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive");
        PendingBalance += amount;
        TotalEarnings += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MovePendingToAvailable(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (PendingBalance < amount) throw new InvalidOperationException("Insufficient pending balance");
        
        PendingBalance -= amount;
        AvailableBalance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeductAvailable(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (AvailableBalance < amount) throw new InvalidOperationException("Insufficient available balance");

        AvailableBalance -= amount;
        TotalWithdrawn += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RefundFromPending(decimal amount)
    {
         if (amount <= 0) throw new ArgumentException("Amount must be positive");
         if (PendingBalance < amount) throw new InvalidOperationException("Insufficient pending balance");

         PendingBalance -= amount;
         TotalEarnings -= amount; // Revert earning
         UpdatedAt = DateTime.UtcNow;
    }

    public void CreditAvailable(decimal amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive");
        AvailableBalance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LockAvailableFunds(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (AvailableBalance < amount) throw new InvalidOperationException("Insufficient available balance");

        AvailableBalance -= amount;
        LockedBalance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnlockFunds(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (LockedBalance < amount) throw new InvalidOperationException("Insufficient locked balance");

        LockedBalance -= amount;
        AvailableBalance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfirmWithdrawal(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (LockedBalance < amount) throw new InvalidOperationException("Insufficient locked balance");

        LockedBalance -= amount;
        TotalWithdrawn += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeductFromLockedForRefund(decimal amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive");
        
        // Trừ tiền từ LockedBalance (cho phép về âm nếu seller chưa có đủ tiền bị giam)
        LockedBalance -= amount;
        TotalRefunded += amount;
        UpdatedAt = DateTime.UtcNow;
    }
}
