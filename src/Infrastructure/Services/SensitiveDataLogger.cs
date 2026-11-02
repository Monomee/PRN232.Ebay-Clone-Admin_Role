using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Infrastructure.Services;

public sealed class SensitiveDataLogger(IApplicationDbContext context, IEncryptionService encryption, IUser user)
    : ISensitiveDataLogger
{
    public void Add(string action, string targetEntity, string? targetId, string classification, object payload)
    {
        var system = user.IsSystem;
        if (!system && (!int.TryParse(user.Id, out var id) || id <= 0))
            throw new UnauthorizedAccessException();
        var plain = JsonSerializer.Serialize(payload);
        context.SensitiveDataLogs.Add(new SensitiveDataLog
        {
            ActorId = system ? 0 : int.Parse(user.Id!),
            ActorUsername = system ? "System" : user.Name,
            ActorRole = system ? "System" : string.Join(",", user.Roles ?? []),
            Action = action,
            TargetEntity = targetEntity,
            TargetId = targetId,
            DataClassification = classification,
            EncryptedData = encryption.Encrypt(plain),
            DataHash = encryption.ComputeHmac(plain),
            EncryptionAlgorithm = "AES-256-GCM",
            KeyIdentifier = "v1",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
