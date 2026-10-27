using System;
using EbayClone.Domain.Common;

namespace EbayClone.Domain.Entities;

public class SensitiveDataLog : BaseAuditableEntity
{
    // Id is inherited from BaseAuditableEntity -> BaseEntity

    // Who performed the action
    public int ActorId { get; set; }

    public string? ActorUsername { get; set; }

    public string? ActorRole { get; set; } // 'SuperAdmin', 'Seller', 'Buyer', 'System'

    // What sensitive operation was performed
    public string Action { get; set; } = string.Empty; // 'UpdatePaymentInfo', 'ResetPassword', 'ExportKYC', 'ChangeAdminRole', 'PayoutExecution', 'WalletAdjustment'

    // What target entity was affected
    public string TargetEntity { get; set; } = string.Empty; // 'User', 'Wallet', 'Payout', 'AdminRole', 'BankAccount'

    public string? TargetId { get; set; }

    // Data classification category
    public string DataClassification { get; set; } = "PII"; // 'PII', 'Financial', 'SecurityCredential', 'AdminPrivilege'

    // The AES-256 encrypted payload stored in database
    public string EncryptedData { get; set; } = string.Empty;

    // Cryptographic metadata
    public string EncryptionAlgorithm { get; set; } = "AES-256-CBC";

    public string KeyIdentifier { get; set; } = "v1";

    public string? DataHash { get; set; } // HMAC-SHA256 for integrity verification

    // Network & client telemetry
    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
