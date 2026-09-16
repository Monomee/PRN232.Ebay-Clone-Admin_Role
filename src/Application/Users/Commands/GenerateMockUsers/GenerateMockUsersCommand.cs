using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;

namespace EbayClone.Application.Users.Commands.GenerateMockUsers;

public record GenerateMockUsersCommand : IRequest<bool>
{
}

public class GenerateMockUsersCommandHandler : IRequestHandler<GenerateMockUsersCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public GenerateMockUsersCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(GenerateMockUsersCommand request, CancellationToken cancellationToken)
    {
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var mockUsers = new List<User>
        {
            // 1. Normal Pending Buyer
            new User
            {
                Username = $"mock_pending_{unixTimestamp}",
                Email = $"mock_pending_{unixTimestamp}@mock.test",
                Role = "Buyer",
                Status = "Active",
                ApprovalStatus = "PendingApproval",
                LastLoginIp = "192.168.10.10",
                Latitude = 21.0,
                Longitude = 105.0,
                CCCD = $"079200{unixTimestamp}",
                PerformanceScore = 100,
                CreatedAt = DateTime.UtcNow
            },
            // 2. Duplicate Pending Buyer (Will clash with user 1 in IP/Coords/CCCD)
            new User
            {
                Username = $"mock_dup_{unixTimestamp}",
                Email = $"mock_dup_{unixTimestamp}@mock.test",
                Role = "Buyer",
                Status = "Active",
                ApprovalStatus = "PendingApproval",
                LastLoginIp = "192.168.10.10", // DUPLICATE IP
                Latitude = 21.0, // DUPLICATE LAT
                Longitude = 105.0, // DUPLICATE LNG
                CCCD = $"079200{unixTimestamp}", // DUPLICATE CCCD
                PerformanceScore = 100,
                CreatedAt = DateTime.UtcNow
            },
            // 3. Active Seller
            new User
            {
                Username = $"mock_seller_{unixTimestamp}",
                Email = $"mock_seller_{unixTimestamp}@mock.test",
                Role = "Seller",
                Status = "Active",
                ApprovalStatus = "Approved",
                ApprovedAt = DateTime.UtcNow,
                LastLoginIp = "8.8.8.8",
                PerformanceScore = 95,
                SellerLevel = "TopRated",
                IsVerified = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            // 4. Banned User
            new User
            {
                Username = $"mock_banned_{unixTimestamp}",
                Email = $"mock_banned_{unixTimestamp}@mock.test",
                Role = "Buyer",
                Status = "Banned",
                ApprovalStatus = "Rejected",
                BannedAt = DateTime.UtcNow.AddDays(-2),
                BannedReason = "Violation of terms - Mock",
                LastLoginIp = "1.2.3.4",
                PerformanceScore = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            }
        };

        _context.Users.AddRange(mockUsers);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
