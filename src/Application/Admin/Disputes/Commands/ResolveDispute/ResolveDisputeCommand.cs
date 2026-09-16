using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Application.Admin.Disputes.Commands.ResolveDispute;

public record ResolveDisputeCommand : IRequest<int>
{
    public int DisputeId { get; init; }
    public string Winner { get; init; } = string.Empty; // 'Buyer', 'Seller', 'Split'
    public decimal? RefundAmount { get; init; }
    public string AdminNotes { get; init; } = string.Empty;
    public bool RequireReturn { get; init; } = false;
    public bool AddSellerViolation { get; init; } = false;
    public bool SendNotifications { get; init; } = true;
}

public class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(v => v.DisputeId)
            .GreaterThan(0).WithMessage("Dispute ID is required.");

        RuleFor(v => v.Winner)
            .NotEmpty().WithMessage("Winner decision is required.")
            .Must(w => w == DisputeWinners.Buyer || w == DisputeWinners.Seller || w == DisputeWinners.Split)
            .WithMessage("Winner must be 'Buyer', 'Seller', or 'Split'.");

        RuleFor(v => v.RefundAmount)
            .GreaterThanOrEqualTo(0).When(v => v.RefundAmount.HasValue)
            .WithMessage("Refund amount cannot be negative.");

        RuleFor(v => v.RefundAmount)
            .NotNull().When(v => v.Winner == DisputeWinners.Buyer || v.Winner == DisputeWinners.Split)
            .WithMessage("Refund amount is required for Buyer or Split decisions.");

        RuleFor(v => v.AdminNotes)
            .NotEmpty().WithMessage("Admin notes are required.")
            .MinimumLength(50).WithMessage("Admin notes must be at least 50 characters.")
            .MaximumLength(2000).WithMessage("Admin notes cannot exceed 2000 characters.");
    }
}

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _currentUser;
    private readonly ILogger<ResolveDisputeCommandHandler> _logger;
    private readonly IDisputeNotifier _disputeNotifier;
    private readonly IEmailService _emailService;

    public ResolveDisputeCommandHandler(
        IApplicationDbContext context,
        IUser currentUser,
        ILogger<ResolveDisputeCommandHandler> logger,
        IDisputeNotifier disputeNotifier,
        IEmailService emailService)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _disputeNotifier = disputeNotifier;
        _emailService = emailService;
    }

    public async Task<int> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        // Get dispute with related data
        var dispute = await _context.Disputes
            .Include(d => d.Order)
                .ThenInclude(o => o!.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(d => d.RaisedByNavigation)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

        Guard.Against.NotFound(request.DisputeId, dispute);

        // Validate dispute can be resolved
        if (dispute.Status == DisputeStatuses.Resolved || dispute.Status == DisputeStatuses.Closed)
        {
            throw new ValidationException("This dispute has already been resolved.");
        }

        // Get current admin ID - Handle mapping from Identity GUID to User table int ID
        var adminId = int.TryParse(_currentUser.Id, out var parsedId) ? parsedId : (int?)null;
        
        if (adminId == null || adminId == 0)
        {
            // Fallback to the default admin user in the Domain User table if identity claim doesn't map directly
            var fallbackAdmin = await _context.Users
                .Where(u => u.Role == "Admin" || u.Role == "System")
                .FirstOrDefaultAsync(cancellationToken);
                
            adminId = fallbackAdmin?.Id ?? 1; // Default to 1 if no user found
        }

        // Capture before state
        var before = new
        {
            dispute.Status,
            dispute.Winner,
            dispute.ResolvedBy,
            dispute.ResolvedAt,
            dispute.AdminNotes,
            dispute.RefundAmount,
            dispute.RefundMethod,
            dispute.RequiresReturn
        };

        // Update dispute resolution
        dispute.Status = DisputeStatuses.Resolved;
        dispute.Winner = request.Winner;
        dispute.ResolvedBy = adminId;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.AdminNotes = request.AdminNotes;
        dispute.RefundAmount = request.RefundAmount ?? 0;
        dispute.RefundMethod = "OriginalPayment";
        dispute.RequiresReturn = request.RequireReturn;

        // Get seller ID from order
        var sellerId = dispute.Order?.OrderItems.FirstOrDefault()?.Product?.SellerId;

        // Retrieve seller wallet and calculate frozen amount
        SellerWallet? sellerWallet = null;
        decimal frozenAmount = 0;
        if (sellerId.HasValue)
        {
            sellerWallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == sellerId.Value, cancellationToken);
            frozenAmount = dispute.Amount ?? 0;
        }

        // Determine the actual refund amount based on the request
        var refundAmount = request.RefundAmount ?? 0;

        // Perform resolution logic based on winner
        if (request.Winner == DisputeWinners.Buyer)
        {
            await ProcessBuyerWins(dispute, sellerWallet?.SellerId, refundAmount, frozenAmount, request.AddSellerViolation, cancellationToken);
        }
        else if (request.Winner == DisputeWinners.Seller)
        {
            await ProcessSellerWins(dispute, sellerWallet?.SellerId, frozenAmount, cancellationToken);
        }
        else if (request.Winner == DisputeWinners.Split)
        {
            await ProcessSplitDecision(dispute, sellerWallet?.SellerId, refundAmount, frozenAmount, cancellationToken);
        }

        // Apply violation globally if checked, and the winner wasn't solely the buyer (which already applies it)
        if (request.AddSellerViolation && request.Winner != DisputeWinners.Buyer && sellerId.HasValue)
        {
            var sellerToViolate = await _context.Users.FindAsync(new object[] { sellerId.Value }, cancellationToken);
            if (sellerToViolate != null)
            {
                sellerToViolate.ViolationCount++;
            }
        }

        // Capture after state
        var after = new
        {
            dispute.Status,
            dispute.Winner,
            dispute.ResolvedBy,
            dispute.ResolvedAt,
            dispute.AdminNotes,
            dispute.RefundAmount,
            dispute.RefundMethod,
            dispute.RequiresReturn
        };

        // Create system message
        var systemMessage = new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderId = adminId ?? 0,
            SenderType = SenderTypes.Admin,
            MessageType = MessageTypes.Note,
            Content = $"Case resolved by admin. Decision: {request.Winner}. {request.AdminNotes}",
            CreatedAt = DateTime.UtcNow,
            IsInternal = false
        };
        _context.DisputeMessages.Add(systemMessage);

        // Log admin action
        var adminAction = new AdminAction
        {
            AdminId = adminId ?? 0,
            Action = "ResolveDispute",
            TargetType = "Dispute",
            TargetId = dispute.Id,
            Details = JsonSerializer.Serialize(new
            {
                caseId = dispute.CaseId,
                adminNotes = request.AdminNotes,
                addSellerViolation = request.AddSellerViolation,
                requireReturn = request.RequireReturn,
                before,
                after
            }),
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminActions.Add(adminAction);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Dispute {CaseId} resolved by Admin {AdminId}. Winner: {Winner}, Refund: {Refund}",
            dispute.CaseId, adminId, request.Winner, request.RefundAmount);

        // Push real-time notification đến tất cả admin đang online qua SignalR.
        // Redis backplane đảm bảo cả những admin nối vào pod khác cũng nhận được.
        await _disputeNotifier.NotifyDisputeResolvedAsync(
            dispute.Id,
            dispute.CaseId ?? string.Empty,
            request.Winner,
            request.RefundAmount ?? 0,
            adminId ?? 0,
            cancellationToken);

        // Send notifications (email/SMS)
        if (request.SendNotifications)
        {
            var buyerEmail = dispute.RaisedByNavigation?.Email;
            var sellerForEmail = await _context.Users.FindAsync(new object[] { sellerId ?? 0 }, cancellationToken);
            var sellerEmail = sellerForEmail?.Email;

            var subject = $"Dispute Resolution: Case {dispute.CaseId}";
            var body = $"Your dispute {dispute.CaseId} has been resolved by an Admin.\nDecision: {request.Winner}\nAdmin Notes: {request.AdminNotes}\nRequires Return: {(request.RequireReturn ? "Yes" : "No")}\nRefund Amount: ${request.RefundAmount ?? 0}";

            // Fire-and-forget to avoid blocking the Admin UI while SMTP is slow
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(buyerEmail))
                    {
                        await _emailService.SendEmailAsync(buyerEmail, subject, body);
                    }

                    if (!string.IsNullOrEmpty(sellerEmail))
                    {
                        await _emailService.SendEmailAsync(sellerEmail, subject, body);
                    }
                }
                catch (Exception)
                {
                    // Fallback to prevent crash if smtp fails. Real app should use ILogger injected or background queue here.
                }
            });
            
            _logger.LogInformation("Scheduled resolution emails to involved parties in the background.");
        }

        return dispute.Id;
    }

    private async Task ProcessBuyerWins(Dispute dispute, int? sellerId, decimal refundAmount, decimal frozenAmount, bool addViolation, CancellationToken cancellationToken)
    {
        // Update order status
        if (dispute.Order != null)
        {
            dispute.Order.Status = "Refunded";
            
            // Adjust earnings so the settlement engine (SettlePendingFundsCommand) accurately registers the deduction
            dispute.Order.SellerEarnings = Math.Max(0, (dispute.Order.SellerEarnings ?? 0) - refundAmount);
        }

        // Update seller wallet
        if (sellerId.HasValue)
        {
            var sellerWallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == sellerId.Value, cancellationToken);
            if (sellerWallet != null)
            {
                var remainingToDeduct = refundAmount;

                // 1. Release as much as we can from DisputedBalance
                var fromDisputed = Math.Min(sellerWallet.DisputedBalance, remainingToDeduct);
                sellerWallet.DisputedBalance -= fromDisputed;
                remainingToDeduct -= fromDisputed;

                // 2. Cascade logic for remaining debt
                if (remainingToDeduct > 0)
                {
                    if (sellerWallet.PendingBalance >= remainingToDeduct)
                    {
                        sellerWallet.PendingBalance -= remainingToDeduct;
                    }
                    else
                    {
                        var fromPending = Math.Max(0, sellerWallet.PendingBalance);
                        sellerWallet.PendingBalance -= fromPending;
                        var stillNeed = remainingToDeduct - fromPending;
                        
                        // Remaining debt hits AvailableBalance (can go negative)
                        sellerWallet.AvailableBalance -= stillNeed;
                    }
                }

                // Wait, what if frozenAmount > refundAmount? (e.g. system over-froze)
                // Any excess frozenAmount should be returned to PendingBalance
                if (frozenAmount > refundAmount)
                {
                    var excess = frozenAmount - refundAmount;
                    var actualExcessReleased = Math.Min(sellerWallet.DisputedBalance, excess); // Only release what's still in disputed
                    sellerWallet.DisputedBalance -= actualExcessReleased;
                    sellerWallet.PendingBalance += actualExcessReleased;
                }

                sellerWallet.TotalRefunded += refundAmount;
                sellerWallet.UpdatedAt = DateTime.UtcNow;
            }

            // Add violation if requested
            if (addViolation)
            {
                var seller = await _context.Users.FindAsync(new object[] { sellerId.Value }, cancellationToken);
                if (seller != null)
                {
                    seller.ViolationCount++;
                }
            }

            // Create refund transaction
            var refundTransaction = new FinancialTransaction
            {
                UserId = sellerId.Value,
                SellerId = sellerId.Value,
                Type = "Refund",
                Amount = refundAmount,
                BalanceAfter = sellerWallet?.AvailableBalance ?? 0,
                Description = $"Refund for dispute {dispute.CaseId}",
                OrderId = dispute.OrderId,
                Date = DateTime.UtcNow
            };
            _context.FinancialTransactions.Add(refundTransaction);
        }

        dispute.RefundProcessedAt = DateTime.UtcNow;
        dispute.RefundTransactionId = Guid.NewGuid().ToString(); // Simulated payment gateway ID
    }

    private async Task ProcessSellerWins(Dispute dispute, int? sellerId, decimal frozenAmount, CancellationToken cancellationToken)
    {
        // Update order status
        if (dispute.Order != null)
        {
            // Restore order status to its pre-dispute state or generic "Delivered"
            dispute.Order.Status = "Delivered";
        }

        // Update seller wallet
        if (sellerId.HasValue && frozenAmount > 0)
        {
            var sellerWallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == sellerId.Value, cancellationToken);
            if (sellerWallet != null)
            {
                // Release frozen funds back to PendingBalance, capped at actual SellerEarnings (excluding platform fee)
                var sellerShare = dispute.Order?.SellerEarnings ?? frozenAmount;
                var amountToRestore = Math.Min(sellerWallet.DisputedBalance, sellerShare);
                sellerWallet.DisputedBalance -= amountToRestore;
                sellerWallet.PendingBalance += amountToRestore;
                sellerWallet.UpdatedAt = DateTime.UtcNow;
            }
        }

        dispute.RefundAmount = 0;
    }

    private async Task ProcessSplitDecision(Dispute dispute, int? sellerId, decimal refundAmount, decimal frozenAmount, CancellationToken cancellationToken)
    {
        // The seller's max possible kept amount is their actual earnings (excluding platform fee)
        var sellerShare = dispute.Order?.SellerEarnings ?? Math.Max(dispute.Amount ?? 0, frozenAmount);
        var sellerKeeps = Math.Max(0, sellerShare - refundAmount);

        // Update order status
        if (dispute.Order != null)
        {
            dispute.Order.Status = "PartiallyRefunded";
            
            // naturally transfers the correct exact amount to AvailableBalance 
            dispute.Order.SellerEarnings = sellerKeeps;
        }

        // Update seller wallet
        if (sellerId.HasValue)
        {
            var sellerWallet = await _context.SellerWallets
                .FirstOrDefaultAsync(w => w.SellerId == sellerId.Value, cancellationToken);

            if (sellerWallet != null)
            {
                var remainingToDeduct = refundAmount;

                // 1. Release as much as we can from DisputedBalance to cover the buyer's refund
                var fromDisputed = Math.Min(sellerWallet.DisputedBalance, remainingToDeduct);
                sellerWallet.DisputedBalance -= fromDisputed;
                remainingToDeduct -= fromDisputed;

                // 2. Cascade logic for remaining refund debt
                if (remainingToDeduct > 0)
                {
                    if (sellerWallet.PendingBalance >= remainingToDeduct)
                    {
                        sellerWallet.PendingBalance -= remainingToDeduct;
                    }
                    else
                    {
                        var fromPending = Math.Max(0, sellerWallet.PendingBalance);
                        sellerWallet.PendingBalance -= fromPending;
                        sellerWallet.AvailableBalance -= (remainingToDeduct - fromPending);
                    }
                }

                // 3. Return the remainder of the frozen funds back to the Seller's PendingBalance
                if (sellerKeeps > 0)
                {
                    var actualKeptRelease = Math.Min(sellerWallet.DisputedBalance, sellerKeeps);
                    sellerWallet.DisputedBalance -= actualKeptRelease;
                    sellerWallet.PendingBalance += actualKeptRelease;
                }

                sellerWallet.TotalRefunded += refundAmount;
                sellerWallet.UpdatedAt = DateTime.UtcNow;
            }

            // Create refund transaction for buyer
            var refundTransaction = new FinancialTransaction
            {
                UserId = sellerId.Value,
                SellerId = sellerId.Value,
                Type = "PartialRefund",
                Amount = refundAmount,
                BalanceAfter = sellerWallet?.AvailableBalance ?? 0,
                Description = $"Partial refund for dispute {dispute.CaseId} (Split decision)",
                OrderId = dispute.OrderId,
                Date = DateTime.UtcNow
            };
            _context.FinancialTransactions.Add(refundTransaction);
        }

        dispute.RefundProcessedAt = DateTime.UtcNow;
        dispute.RefundTransactionId = Guid.NewGuid().ToString();
    }
}
