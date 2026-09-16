
﻿using EbayClone.Domain.Entities;

using Microsoft.EntityFrameworkCore;


using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Common.Interfaces;

public interface IApplicationDbContext
{

    // 1. Khai báo các bảng (DbSet) mà tầng Application được phép truy cập
    DbSet<Address> Addresses { get; }
    DbSet<Bid> Bids { get; }
    DbSet<Category> Categories { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<Feedback> Feedbacks { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<Message> Messages { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderTable> OrderTables { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Product> Products { get; }
    DbSet<ReturnRequest> ReturnRequests { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ReviewReport> ReviewReports { get; }
    DbSet<ShippingInfo> ShippingInfos { get; }
    DbSet<Store> Stores { get; }

    // Lưu ý: User trong Domain khác với ApplicationUser của Identity
    // Nếu bạn dùng chung, cần cẩn thận reference. 
    // Nhưng theo code bạn gửi thì User là Entity của Domain.
    DbSet<User> Users { get; }

    // Các bảng mới cho Admin
    DbSet<ProductReport> ProductReports { get; }
    DbSet<SellerWallet> SellerWallets { get; }

    DbSet<PlatformFee> PlatformFees { get; }
    DbSet<AdminAction> AdminActions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<UserNotificationRead> UserNotificationReads { get; }
    DbSet<AdminRole> AdminRoles { get; }
    DbSet<AdminUserRole> AdminUserRoles { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<DisputeMessage> DisputeMessages { get; }
    DbSet<PayoutConfig> PayoutConfigs { get; }
    DbSet<PayoutTransaction> PayoutTransactions { get; }
    DbSet<SellerLevelCriteria> SellerLevelCriteria { get; }
    DbSet<WithdrawalRequest> WithdrawalRequests { get; }


    // 2. Hàm lưu thay đổi (Quan trọng nhất)

  
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

