using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.Users.Queries.GetUserById;

public record GetUserByIdQuery(int Id) : IRequest<UserDto?>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryption;
    private readonly ISensitiveDataLogger _sensitiveLog;

    private readonly IUser _currentUser;

    public GetUserByIdQueryHandler(IApplicationDbContext context, IUser currentUser, IEncryptionService encryption, ISensitiveDataLogger sensitiveLog)
    {
        _context = context;
        _encryption = encryption;
        _sensitiveLog = sensitiveLog;

        _currentUser = currentUser;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.Addresses)
            .Include(u => u.OrderTables)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            return null;
        }

        if (!int.TryParse(_currentUser.Id, out var actorId) || actorId <= 0) throw new UnauthorizedAccessException();
        if (_currentUser.Roles?.Any(r => r is "Support" or "SuperAdmin") != true) throw new EbayClone.Application.Common.Exceptions.ForbiddenAccessException();

        var dto = user.ToDto();

        dto.MaskedPhone = MaskPhone(user.Addresses
            .OrderByDescending(a => a.IsDefault == true)
            .ThenByDescending(a => a.Id)
            .Select(a => a.Phone)
            .FirstOrDefault());

        var documents = user.VerificationDocuments == null ? null : _encryption.Decrypt(user.VerificationDocuments);
        var nationalId = ExtractNationalId(documents) ?? (user.CCCD == null ? null : _encryption.Decrypt(user.CCCD));
        dto.MaskedNationalId = MaskNationalId(nationalId);

        dto.OrderHistory = user.OrderTables
            .OrderByDescending(o => o.OrderDate)
            .Take(10)
            .Select(o => new UserOrderHistoryDto
            {
                OrderId = o.Id,
                OrderDate = o.OrderDate,
                Status = o.Status,
                TotalPrice = o.TotalPrice
            })
            .ToList();

        dto.ViolationHistory = await _context.AdminActions
            .Where(a => a.TargetType == "User" && a.TargetId == request.Id)
            .Where(a => a.Action.Contains("Ban") || a.Action.Contains("Reject") || a.Action.Contains("Suspend"))
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new UserViolationHistoryDto
            {
                AuditLogId = a.Id,
                Action = a.Action,
                Details = a.Details,
                CreatedAt = a.CreatedAt,
                AdminId = a.AdminId,
                AdminUsername = a.Admin != null ? a.Admin.Username : null
            })
            .ToListAsync(cancellationToken);

        if (int.TryParse(_currentUser.Id, out var adminId))
        {
            _context.AdminActions.Add(new AdminAction
            {
                AdminId = adminId,
                Action = "ViewUserProfile",
                TargetType = "User",
                TargetId = request.Id,
                Details = JsonSerializer.Serialize(new
                {
                    viewedFields = new[] { "PersonalInfo", "OrderHistory", "ViolationHistory", "MaskedSensitiveData" }
                }),
                CreatedAt = DateTime.UtcNow
            });

            _sensitiveLog.Add("ViewUserProfile", "User", user.Id.ToString(), "PII", new { fields = new[] { "MaskedNationalId", "MaskedPhone" } });
            await _context.SaveChangesAsync(cancellationToken);
        }

        return dto;
    }

    private static string? ExtractNationalId(string? verificationDocuments)
    {
        if (string.IsNullOrWhiteSpace(verificationDocuments))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(verificationDocuments);
            var root = json.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var keys = new[] { "cccd", "nationalId", "idNumber", "citizenId" };
                foreach (var key in keys)
                {
                    if (root.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
                    {
                        return property.GetString();
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed JSON from legacy data.
        }

        return null;
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
        {
            return new string('*', digits.Length);
        }

        return $"{digits[..2]}{new string('*', digits.Length - 4)}{digits[^2..]}";
    }

    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
        {
            return null;
        }

        if (nationalId.Length <= 4)
        {
            return new string('*', nationalId.Length);
        }

        return $"{nationalId[..2]}{new string('*', nationalId.Length - 4)}{nationalId[^2..]}";
    }
}
