using EbayClone.Domain.Entities;
using FluentValidation.Results;
using ValidationException = EbayClone.Application.Common.Exceptions.ValidationException;

namespace EbayClone.Application.Users.Risk;

/// Who may act on the marketplace (list, review, cash out). A clone account is most useful in the window
/// between sign-up and the risk review, so "not reviewed yet" is blocked the same as suspended.
public static class AccountStanding
{
    public static string? Problem(User? user) => user switch
    {
        null => "Không tìm thấy tài khoản.",
        _ when AccountLinking.IsBanned(user) => "Tài khoản của bạn đã bị khóa.",
        { Status: "Suspended" } => "Tài khoản của bạn đang bị tạm ngưng.",
        { ApprovalStatus: "PendingApproval" } or { Status: "Pending" } => "Tài khoản của bạn đang chờ duyệt.",
        _ => null,
    };

    /// Throws a 400 when the account may not act; requiredRole (e.g. "Seller") also checks the role.
    public static void Ensure(User? user, string? requiredRole = null)
    {
        var problem = Problem(user);
        if (problem == null && requiredRole != null && user!.Role != requiredRole)
            problem = $"Chỉ tài khoản {requiredRole} mới được thực hiện thao tác này.";
        if (problem != null) throw new ValidationException([new ValidationFailure("Account", problem)]);
    }
}
