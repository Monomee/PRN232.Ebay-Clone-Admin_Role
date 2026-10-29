using System;

namespace EbayClone.Application.SensitiveLogs;

public record SensitiveLogDto
{
    public int Id { get; init; }
    public int ActorId { get; init; }
    public string? ActorUsername { get; init; }
    public string? ActorRole { get; init; }
    public string Action { get; init; } = string.Empty;
    public string TargetEntity { get; init; } = string.Empty;
    public string? TargetId { get; init; }
    public string DataClassification { get; init; } = "PII";
    
    // The encrypted ciphertext stored in DB (Base64)
    public string EncryptedData { get; init; } = string.Empty;

    public string EncryptionAlgorithm { get; init; } = "AES-256-GCM";
    public string KeyIdentifier { get; init; } = "v1";
    public string? DataHash { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    // Plaintext decrypted data (only filled when explicitly requested through Decrypt query)
    public string? DecryptedData { get; init; }
}

public record DecryptedSensitiveLogResult
{
    public int Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string DataClassification { get; init; } = string.Empty;
    public string EncryptedData { get; init; } = string.Empty;
    public string DecryptedData { get; init; } = string.Empty;
    public string EncryptionAlgorithm { get; init; } = "AES-256-GCM";
    public string? DataHash { get; init; }
    public bool IntegrityVerified { get; init; }
    public DateTimeOffset DecryptedAt { get; init; }
    public string DecryptedBy { get; init; } = string.Empty;
}

public record SensitiveLogsSummaryDto
{
    public int TotalLogs { get; init; }
    public int PiiCount { get; init; }
    public int FinancialCount { get; init; }
    public int SecurityCredentialCount { get; init; }
    public int AdminPrivilegeCount { get; init; }
}
