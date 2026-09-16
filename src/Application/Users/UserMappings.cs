using System.Linq.Expressions;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users;

public static class UserMappings
{
    public static Expression<Func<User, UserBriefDto>> Projection =>
        user => new UserBriefDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            ApprovalStatus = user.ApprovalStatus,
            IsVerified = user.IsVerified,
            LastLoginAt = user.LastLoginAt
        };

    public static UserDto ToDto(this User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            ApprovalStatus = user.ApprovalStatus,
            BannedReason = user.BannedReason,
            BannedBy = user.BannedBy,
            BannedAt = user.BannedAt,
            ApprovedBy = user.ApprovedBy,
            ApprovedAt = user.ApprovedAt,
            TwoFactorEnabled = user.TwoFactorEnabled,
            IsVerified = user.IsVerified,
            ViolationCount = user.ViolationCount,
            LastLoginIp = user.LastLoginIp,
            LastLoginAt = user.LastLoginAt,
            SellerLevel = user.SellerLevel
        };
    }

    public static UserBriefDto ToBriefDto(this User user)
    {
        return new UserBriefDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            ApprovalStatus = user.ApprovalStatus,
            IsVerified = user.IsVerified,
            LastLoginAt = user.LastLoginAt,
            SellerLevel = user.SellerLevel
        };
    }
}
