using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Data.Seeders;

public class SensitiveDataLogsSeeder : ISeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger _logger;
    private readonly IEncryptionService _encryptionService;

    public int Order => 16; // Chạy sau Users và Payouts

    public SensitiveDataLogsSeeder(ApplicationDbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
        _encryptionService = new AesEncryptionService(new ConfigurationBuilder().Build());
    }

    public async Task SeedAsync()
    {
        // 1. Tự động kiểm tra và đảm bảo schema bảng SensitiveDataLogs chính xác
        var ensureTableSql = @"
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'SensitiveDataLogs' AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SensitiveDataLogs') AND name = 'CreatedAt'))
            BEGIN
                DROP TABLE SensitiveDataLogs;
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SensitiveDataLogs')
            BEGIN
                CREATE TABLE SensitiveDataLogs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ActorId INT NOT NULL,
                    ActorUsername NVARCHAR(256) NULL,
                    ActorRole NVARCHAR(50) NULL,
                    Action NVARCHAR(100) NOT NULL,
                    TargetEntity NVARCHAR(100) NOT NULL,
                    TargetId NVARCHAR(100) NULL,
                    DataClassification NVARCHAR(50) NOT NULL,
                    EncryptedData NVARCHAR(MAX) NOT NULL,
                    EncryptionAlgorithm NVARCHAR(50) NOT NULL,
                    KeyIdentifier NVARCHAR(50) NOT NULL,
                    DataHash NVARCHAR(256) NULL,
                    IpAddress NVARCHAR(50) NULL,
                    UserAgent NVARCHAR(500) NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    CreatedBy NVARCHAR(256) NULL,
                    LastModified DATETIMEOFFSET NULL,
                    LastModifiedBy NVARCHAR(256) NULL
                );
            END";
        await _context.Database.ExecuteSqlRawAsync(ensureTableSql);

        if (await _context.SensitiveDataLogs.AnyAsync())
        {
            return;
        }

        _logger.LogInformation("Seeding encrypted SensitiveDataLogs...");

        var samplePayloads = new[]
        {
            new {
                Action = "UpdatePaymentInfo",
                TargetEntity = "BankAccount",
                TargetId = "ACC_8829",
                Classification = "Financial",
                ActorId = 2,
                ActorName = "tech_seller_pro",
                ActorRole = "Seller",
                Ip = "192.168.1.102",
                Json = "{\"bankName\": \"Techcombank\", \"accountNumber\": \"19034567891011\", \"accountHolder\": \"NGUYEN HOANG NAM\", \"branch\": \"Ha Noi\", \"swiftCode\": \"TCBVNVX\"}"
            },
            new {
                Action = "ExportKYC_Identity",
                TargetEntity = "User",
                TargetId = "3",
                Classification = "PII",
                ActorId = 1,
                ActorName = "superadmin@ebay.local",
                ActorRole = "SuperAdmin",
                Ip = "127.0.0.1",
                Json = "{\"citizenId\": \"001202008899\", \"fullName\": \"TRAN THI MAI\", \"dob\": \"1995-08-20\", \"address\": \"Cau Giay, Ha Noi\", \"docType\": \"CCCD_CHIP\", \"verificationStatus\": \"Verified\"}"
            },
            new {
                Action = "ResetPassword",
                TargetEntity = "User",
                TargetId = "5",
                Classification = "SecurityCredential",
                ActorId = 1,
                ActorName = "superadmin@ebay.local",
                ActorRole = "SuperAdmin",
                Ip = "127.0.0.1",
                Json = "{\"requestedBy\": \"AdminHelpdesk\", \"temporaryPasswordHash\": \"$2a$12$N9qo8uLOickgx2ZMRZoMye.I...\", \"mustChangeOnNextLogin\": true, \"channel\": \"SMS_OTP\"}"
            },
            new {
                Action = "ChangeAdminRole",
                TargetEntity = "AdminRole",
                TargetId = "ROLE_SUPPORT",
                Classification = "AdminPrivilege",
                ActorId = 1,
                ActorName = "superadmin@ebay.local",
                ActorRole = "SuperAdmin",
                Ip = "127.0.0.1",
                Json = "{\"targetUserId\": 4, \"targetUsername\": \"support_agent_01\", \"previousRoles\": [\"Monitor\"], \"grantedRoles\": [\"Monitor\", \"Support\"], \"authorizer\": \"SuperAdmin\"}"
            },
            new {
                Action = "PayoutExecution",
                TargetEntity = "PayoutTransaction",
                TargetId = "PO_991823",
                Classification = "Financial",
                ActorId = 1,
                ActorName = "PayoutEngineSystem",
                ActorRole = "System",
                Ip = "127.0.0.1",
                Json = "{\"sellerId\": 2, \"payoutAmount\": 3500.00, \"currency\": \"USD\", \"bankRouting\": \"987654321\", \"gatewayConfirmation\": \"BANK_MOCK_SUCCESS_7781\"}"
            },
            new {
                Action = "WalletAdjustment",
                TargetEntity = "SellerWallet",
                TargetId = "WLT_104",
                Classification = "Financial",
                ActorId = 1,
                ActorName = "superadmin@ebay.local",
                ActorRole = "SuperAdmin",
                Ip = "127.0.0.1",
                Json = "{\"sellerId\": 2, \"adjustmentAmount\": -150.00, \"balanceType\": \"LockedBalance\", \"reason\": \"Hold funds due to pending Vero dispute claim #89\", \"approvedBy\": \"SuperAdmin\"}"
            },
            new {
                Action = "TwoFactorSecretGenerated",
                TargetEntity = "UserSecurity",
                TargetId = "2FA_USR_2",
                Classification = "SecurityCredential",
                ActorId = 2,
                ActorName = "tech_seller_pro",
                ActorRole = "Seller",
                Ip = "192.168.1.102",
                Json = "{\"totpAlgorithm\": \"SHA1\", \"secretKey\": \"JBSWY3DPEHPK3PXP\", \"recoveryCodes\": [\"109-283\", \"482-910\", \"847-192\", \"503-847\"], \"createdAt\": \"2026-10-25T14:30:00Z\"}"
            },
            new {
                Action = "IpWhitelistModified",
                TargetEntity = "SystemAdmin",
                TargetId = "SYS_CONFIG_IP",
                Classification = "AdminPrivilege",
                ActorId = 1,
                ActorName = "superadmin@ebay.local",
                ActorRole = "SuperAdmin",
                Ip = "127.0.0.1",
                Json = "{\"policy\": \"StrictInternalIps\", \"previousIps\": [\"127.0.0.1\", \"::1\"], \"updatedIps\": [\"127.0.0.1\", \"::1\", \"192.168.1.*\", \"10.*\"], \"scope\": \"AdminPortal\"}"
            }
        };

        var logs = new List<SensitiveDataLog>();

        for (int i = 0; i < samplePayloads.Length; i++)
        {
            var item = samplePayloads[i];
            
            // Mã hóa dữ liệu nhạy cảm bằng AES-256
            string encrypted = _encryptionService.Encrypt(item.Json);
            // Tính toán HMAC để kiểm tra toàn vẹn
            string hash = _encryptionService.ComputeHmac(item.Json);

            logs.Add(new SensitiveDataLog
            {
                ActorId = item.ActorId,
                ActorUsername = item.ActorName,
                ActorRole = item.ActorRole,
                Action = item.Action,
                TargetEntity = item.TargetEntity,
                TargetId = item.TargetId,
                DataClassification = item.Classification,
                EncryptedData = encrypted,
                EncryptionAlgorithm = "AES-256-CBC",
                KeyIdentifier = "v1",
                DataHash = hash,
                IpAddress = item.Ip,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SuperAdmin-Console/2.0",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-(i * 3 + 1))
            });
        }

        _context.SensitiveDataLogs.AddRange(logs);
        await _context.SaveChangesAsync(default);

        _logger.LogInformation("Successfully seeded {Count} encrypted SensitiveDataLogs.", logs.Count);
    }
}
