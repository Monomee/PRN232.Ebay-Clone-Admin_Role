using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EbayClone.Infrastructure.Data.Interceptors;

/// <summary>
/// Intercepts any SaveChanges that modifies SellerWallet rows
/// and broadcasts the updated balances to all admin clients via SignalR.
/// This is the single source of truth for real-time updates — no matter
/// which code path mutates a wallet, admins on /sellers will see it instantly.
/// </summary>
public class SellerWalletChangedInterceptor : SaveChangesInterceptor
{
    private readonly ISellerHubService _sellerHubService;
    private readonly AsyncLocal<List<SellerWallet>?> _changedWallets = new();

    public SellerWalletChangedInterceptor(ISellerHubService sellerHubService)
    {
        _sellerHubService = sellerHubService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CaptureWalletChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CaptureWalletChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await BroadcastWalletChanges();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        BroadcastWalletChanges().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    private void CaptureWalletChanges(DbContext? context)
    {
        if (context == null) return;

        var changedWallets = context.ChangeTracker
            .Entries<SellerWallet>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        // Create detached clones or just keep references. Since we don't need navigation properties, references are fine
        // as long as their properties have the new values.
        _changedWallets.Value = changedWallets;
    }

    private async Task BroadcastWalletChanges()
    {
        var walletsToBroadcast = _changedWallets.Value;
        if (walletsToBroadcast == null || !walletsToBroadcast.Any()) return;

        foreach (var wallet in walletsToBroadcast)
        {
            try
            {
                await _sellerHubService.BroadcastWalletUpdate(
                    wallet.SellerId,
                    wallet.AvailableBalance,
                    wallet.PendingBalance,
                    wallet.TotalWithdrawn);
            }
            catch
            {
                // Never let SignalR failures affect the main transaction
            }
        }

        _changedWallets.Value = null; // Clean up
    }
}
