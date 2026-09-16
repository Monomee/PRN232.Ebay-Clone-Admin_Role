using EbayClone.Domain.Constants;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

public class FinanceDemoSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FinanceDemoSeeder> _logger;

    public int Order => 10; 

    public FinanceDemoSeeder(ApplicationDbContext context, ILogger<FinanceDemoSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // 1. Setup Demo Data for Settlement (Pending -> Available)
        // Settlement is now handled by SettlementBackgroundService
        _logger.LogInformation("FinanceDemoSeeder: No longer seeding manual withdrawals. Payout Engine handles this.");
        await Task.CompletedTask;
    }
}
