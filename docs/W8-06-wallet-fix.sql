-- W8-06 / z8rp3fcgj0: cộng tiền cho order Delivered của Case 5 (return request "Camera lens cracked")
-- mà ReturnRequestsSeeder cũ tạo ra nhưng chưa cộng vào ví seller.
-- Chạy được nhiều lần: nếu order đã có dòng FeeDeduction thì không làm gì.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @OrderId int, @SellerId int, @Earnings decimal(18, 2), @Fee decimal(18, 2), @Pending decimal(18, 2);

SELECT TOP 1 @OrderId = o.Id, @Earnings = o.SellerEarnings, @Fee = o.PlatformFee
FROM OrderTable o
JOIN ReturnRequest r ON r.OrderId = o.Id
WHERE r.Reason LIKE N'%Camera lens cracked%' AND o.Status = 'Delivered';

SELECT TOP 1 @SellerId = p.SellerId
FROM OrderItem oi JOIN Product p ON p.Id = oi.ProductId
WHERE oi.OrderId = @OrderId;

IF @OrderId IS NULL
    PRINT N'Không có order Case 5 ở trạng thái Delivered: không cần sửa.';
ELSE IF EXISTS (SELECT 1 FROM FinancialTransactions WHERE OrderId = @OrderId AND Type = 'FeeDeduction')
    PRINT CONCAT(N'Order #', @OrderId, N' đã được cộng vào ví trước đó: không làm gì.');
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM SellerWallets WHERE SellerId = @SellerId)
        THROW 50000, N'Seller chưa có ví: dừng, không sửa gì.', 1;

    UPDATE SellerWallets
    SET PendingBalance = PendingBalance + @Earnings,
        TotalEarnings = TotalEarnings + @Earnings,
        @Pending = PendingBalance + @Earnings,
        UpdatedAt = SYSUTCDATETIME()
    WHERE SellerId = @SellerId;

    -- Giống GenerateMockOrder: settle sau 21 ngày, hạn dispute 14 ngày kể từ lúc giao
    UPDATE OrderTable
    SET EstimatedSettlementDate = COALESCE(EstimatedSettlementDate, DATEADD(day, 21, CompletedAt)),
        CanDisputeUntil = COALESCE(CanDisputeUntil, DATEADD(day, 14, CompletedAt))
    WHERE Id = @OrderId;

    INSERT INTO FinancialTransactions (SellerId, UserId, Type, Amount, BalanceAfter, OrderId, Description, Date)
    VALUES (@SellerId, @SellerId, 'FeeDeduction', @Fee, @Pending, @OrderId, CONCAT('Final value fee for order #', @OrderId), SYSUTCDATETIME());

    PRINT CONCAT(N'Đã cộng ', @Earnings, N' vào ví seller #', @SellerId, N' cho order #', @OrderId, N'. PendingBalance mới: ', @Pending);
END

COMMIT;
