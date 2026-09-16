using EbayClone.Application.PlatformFees.Commands.UpdatePlatformFees;
using EbayClone.Application.PlatformFees.Queries.GetPlatformFees;
using EbayClone.Application.Reports.Queries.GetRevenueReport;
using EbayClone.Application.Financials.Commands.SettlePendingFunds;
using EbayClone.Application.Financials.Queries.GetPendingSettlementOrders;
using EbayClone.Application.Financials.Queries.GetSellerPendingFunds;
using EbayClone.Application.Financials.Commands.SettleOrder;
using EbayClone.Application.Payouts.Commands.RunPayoutEngine;
using EbayClone.Application.Payouts.Commands.ReleaseHold;
using EbayClone.Application.Payouts.Commands.UpdatePayoutConfig;
using EbayClone.Application.Payouts.Queries.GetPayoutHistory;
using EbayClone.Application.Payouts.Queries.GetPayoutExceptions;
using EbayClone.Application.Payouts.Queries.GetPayoutConfig;
using EbayClone.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using EbayClone.Web.Infrastructure;

namespace EbayClone.Web.Endpoints;

public class Financials : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        // Fees
        group.MapGet("fees", GetFees)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));
             
        group.MapPut("fees", UpdateFees)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        // Maintenance/Settlement
        group.MapPost("trigger-settlement", TriggerSettlement);
        group.MapGet("settlement-orders", GetPendingSettlementOrders);
        group.MapPost("settle-order/{id}", SettleOrder)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapGet("pending/{sellerId:int}", GetSellerPendingFunds);

        // ── Automated Payout Engine ───────────────────────────────────────────
        group.MapPost("payouts/run", RunPayoutEngine)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapPost("payouts/{id}/release-hold", ReleaseHold)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapGet("payouts/history", GetPayoutHistory)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapGet("payouts/exceptions", GetPayoutExceptions)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapGet("payouts/config", GetPayoutConfig)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        group.MapPut("payouts/config", UpdatePayoutConfig)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator, Roles.SuperAdmin));

        // Reports
        group.MapGet("reports/revenue", GetRevenueReport)
             .RequireAuthorization(policy => policy.RequireRole(Roles.Administrator));
    }

    public async Task<PlatformFeesDto> GetFees(ISender sender)
    {
        return await sender.Send(new GetPlatformFeesQuery());
    }

    public async Task<int> UpdateFees(ISender sender, UpdatePlatformFeesCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<int> TriggerSettlement(ISender sender)
    {
        return await sender.Send(new SettlePendingFundsCommand());
    }

    public async Task<List<PendingSettlementOrderDto>> GetPendingSettlementOrders(ISender sender)
    {
        return await sender.Send(new GetPendingSettlementOrdersQuery());
    }

    public async Task<IResult> SettleOrder(ISender sender, int id)
    {
        var result = await sender.Send(new SettleOrderCommand(id));
        return result ? Results.Ok() : Results.BadRequest("Failed to settle order. Ensure it's delivered and past dispute window.");
    }

    public async Task<RevenueReportDto> GetRevenueReport(ISender sender)
    {
        return await sender.Send(new GetRevenueReportQuery());
    }

    public async Task<List<SellerPendingFundDto>> GetSellerPendingFunds(ISender sender, int sellerId)
    {
        return await sender.Send(new GetSellerPendingFundsQuery(sellerId));
    }

    // ── Payout Engine handlers ────────────────────────────────────────────────
    public async Task<PayoutEngineResult> RunPayoutEngine(ISender sender)
    {
        return await sender.Send(new RunPayoutEngineCommand());
    }

    public async Task<IResult> ReleaseHold(ISender sender, int id)
    {
        var success = await sender.Send(new ReleaseHoldCommand(id));
        return success ? Results.NoContent() : Results.NotFound("Payout transaction not found or not in Hold/Failed state.");
    }

    public async Task<List<PayoutHistoryDto>> GetPayoutHistory(ISender sender, [FromQuery] string groupBy = "day")
    {
        return await sender.Send(new GetPayoutHistoryQuery(groupBy));
    }

    public async Task<List<PayoutExceptionDto>> GetPayoutExceptions(ISender sender)
    {
        return await sender.Send(new GetPayoutExceptionsQuery());
    }

    public async Task<PayoutConfigDto> GetPayoutConfig(ISender sender)
    {
        return await sender.Send(new GetPayoutConfigQuery());
    }

    public async Task<IResult> UpdatePayoutConfig(ISender sender, UpdatePayoutConfigCommand command)
    {
        await sender.Send(command);
        return Results.NoContent();
    }
}
