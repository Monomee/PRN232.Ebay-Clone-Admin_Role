using System;
using System.Threading;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Application.SensitiveLogs.Queries.DecryptSensitiveLog;

public record DecryptSensitiveLogQuery(int Id) : IRequest<DecryptedSensitiveLogResult?>;

public class DecryptSensitiveLogQueryHandler : IRequestHandler<DecryptSensitiveLogQuery, DecryptedSensitiveLogResult?>
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IUser _currentUser;

    public DecryptSensitiveLogQueryHandler(
        IApplicationDbContext context, 
        IEncryptionService encryptionService,
        IUser currentUser)
    {
        _context = context;
        _encryptionService = encryptionService;
        _currentUser = currentUser;
    }

    public async Task<DecryptedSensitiveLogResult?> Handle(DecryptSensitiveLogQuery request, CancellationToken cancellationToken)
    {
        var log = await _context.SensitiveDataLogs
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken);

        if (log == null) return null;

        // Giải mã dữ liệu từ AES-256 ciphertext
        string decryptedText = _encryptionService.Decrypt(log.EncryptedData);

        // Kiểm tra tính toàn vẹn (HMAC-SHA256)
        string computedHash = _encryptionService.ComputeHmac(decryptedText);
        bool integrityVerified = !string.IsNullOrEmpty(log.DataHash) && log.DataHash.Equals(computedHash, StringComparison.OrdinalIgnoreCase);

        // Ghi lại Audit Trail về hành động giải mã dữ liệu nhạy cảm của SuperAdmin
        int currentUserId = int.TryParse(_currentUser.Id, out var parsedId) ? parsedId : 1;
        _context.AdminActions.Add(new AdminAction
        {
            AdminId = currentUserId,
            Action = "DecryptSensitiveDataLog",
            TargetType = "SensitiveDataLog",
            TargetId = log.Id,
            Details = $"{{\"logId\":{log.Id},\"action\":\"{log.Action}\",\"classification\":\"{log.DataClassification}\"}}",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        return new DecryptedSensitiveLogResult
        {
            Id = log.Id,
            Action = log.Action,
            DataClassification = log.DataClassification,
            EncryptedData = log.EncryptedData,
            DecryptedData = decryptedText,
            EncryptionAlgorithm = log.EncryptionAlgorithm,
            DataHash = log.DataHash,
            IntegrityVerified = integrityVerified,
            DecryptedAt = DateTime.UtcNow,
            DecryptedBy = _currentUser.Id ?? "SuperAdmin"
        };
    }
}
