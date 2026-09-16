using EbayClone.Application.Common.Exceptions;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.ReturRequests.Commands.ApproveReturnRequest;
public record ApproveReturnRequestCommand(
    int ReturnRequestId, 
    string? AdminNote,
    string? ResolutionAction = "KeepItem", // "RequireReturn", "KeepItem", "RefundWithoutReturn"
    bool IsRefundedByEbayFund = false)
    : IRequest<Unit>;

public class ApproveReturnRequestCommandHandler
    : IRequestHandler<ApproveReturnRequestCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationNotifier _notifier;
    private readonly IUser _user;

    public ApproveReturnRequestCommandHandler(IApplicationDbContext context, INotificationNotifier notifier, IUser user)
    {
        _context = context;
        _notifier = notifier;
        _user = user;
    }

    public async Task<Unit> Handle(
        ApproveReturnRequestCommand request,
        CancellationToken cancellationToken)
    {
        var returnRequest = await _context.ReturnRequests
            .Include(r => r.Order)
                .ThenInclude(o => o!.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(r => r.User) // Buyer
            .FirstOrDefaultAsync(r => r.Id == request.ReturnRequestId, cancellationToken);

        if (returnRequest == null)
            throw new NotFoundException(nameof(ReturnRequest), $"{request.ReturnRequestId}");

        if (returnRequest.Status != ReturnStatuses.Pending && returnRequest.Status != ReturnStatuses.Escalated && returnRequest.Status != ReturnStatuses.Frozen)
            throw new InvalidOperationException(
                $"Yêu cầu hoàn trả #{request.ReturnRequestId} không ở trạng thái hợp lệ để approve (Status: {returnRequest.Status}).");

        // Cập nhật ReturnRequest
        returnRequest.AdminNote = request.AdminNote;
        returnRequest.ResolvedAt = DateTime.UtcNow;
        if (int.TryParse(_user.Id, out int adminId))
        {
            returnRequest.ResolvedByAdminId = adminId;
        }
        returnRequest.ResolutionAction = request.ResolutionAction;
        returnRequest.IsRefundedByEbayFund = request.IsRefundedByEbayFund;

        if (request.ResolutionAction == "RequireReturn")
        {
            returnRequest.Status = ReturnStatuses.WaitingForReturnLabel; // Đợi nhãn trả hàng
        }
        else 
        {
            // "KeepItem" hoặc "RefundWithoutReturn" -> Hoàn tiền ngay lập tức
            returnRequest.Status = ReturnStatuses.Approved;

            // Cập nhật trạng thái đơn hàng liên quan
            if (returnRequest.Order != null)
            {
                returnRequest.Order.Status = "Refunded";

                decimal refundAmount = returnRequest.Order.SellerEarnings ?? returnRequest.Order.TotalPrice ?? 0m;

                // 2. Trừ tiền từ LockedBalance của Seller nếu không phải Ebay đền bù
                if (!request.IsRefundedByEbayFund)
                {
                    int? sellerId = returnRequest.Order.OrderItems.FirstOrDefault()?.Product?.SellerId;
                    if (sellerId.HasValue)
                    {
                        var sellerWallet = await _context.SellerWallets
                            .FirstOrDefaultAsync(w => w.SellerId == sellerId.Value, cancellationToken);

                        if (sellerWallet != null)
                        {
                            // Kiểm tra số dư trong LockedBalance (Yêu cầu nghiệp vụ mới)
                            if (sellerWallet.LockedBalance < refundAmount)
                            {
                                throw new InvalidOperationException($"Số dư LockedBalance của Seller (#{sellerId}) không đủ để thực hiện hoàn tiền (Cần: {refundAmount}, Có: {sellerWallet.LockedBalance}).");
                            }

                            if (refundAmount > 0)
                            {
                                sellerWallet.DeductFromLockedForRefund(refundAmount);
                                
                                // 3. Tạo PayoutTransaction để lưu vết (Status: Refund)
                                var payoutTx = new PayoutTransaction
                                {
                                    SellerId = sellerId.Value,
                                    Amount = refundAmount,
                                    Status = PayoutTransaction.StatusRefund,
                                    CreatedAt = DateTime.UtcNow,
                                    CompletedAt = DateTime.UtcNow,
                                    ErrorLog = $"Refund for ReturnRequest #{returnRequest.Id}",
                                    SessionId = $"REFUND-{returnRequest.Id}"
                                };
                                _context.PayoutTransactions.Add(payoutTx);
                            }
                        }
                    }
                }
                
                // Ghi log Admin Action
                var adminAction = new AdminAction
                {
                    AdminId = int.Parse(_user.Id ?? "0"),
                    Action = "ApproveReturnRequest",
                    TargetType = "ReturnRequest",
                    TargetId = returnRequest.Id,
                    Details = $"Approved return request #{returnRequest.Id}. Refund amount: ${refundAmount}. Resolution: {request.ResolutionAction}. eBay Funded: {request.IsRefundedByEbayFund}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AdminActions.Add(adminAction);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        
        // Broadcast update
        await _notifier.NotifyReturnRequestUpdatedAsync(returnRequest.Id, returnRequest.Status, cancellationToken);

        return Unit.Value;
    }
}
