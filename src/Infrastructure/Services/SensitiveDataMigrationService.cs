using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EbayClone.Infrastructure.Services;

public record SensitiveDataMigrationResult(int PiiFields, int Logs, int LookupHashes)
{
    public bool NeedsConversion => PiiFields + Logs + LookupHashes > 0;
}

public sealed class SensitiveDataMigrationService(ApplicationDbContext context, IEncryptionService encryption)
{
    public async Task<SensitiveDataMigrationResult> RunAsync(bool apply, CancellationToken ct = default)
    {
        if ((await context.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Apply the reviewed EF schema migration before sensitive-data conversion.");

        await using var transaction = apply
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var changes = new List<Action>();
        var verify = new List<Action>();
        int fields = 0, logs = 0, hashes = 0;

        // shortcut: validate the complete demo dataset in memory; batch this before using a large production DB.
        var users = await context.Users.ToListAsync(ct);
        var payouts = await context.PayoutTransactions.ToListAsync(ct);
        var withdrawals = await context.WithdrawalRequests.ToListAsync(ct);
        var sensitiveLogs = await context.SensitiveDataLogs.ToListAsync(ct);

        string? Protect(string? value, Action<string> set, bool json = false)
        {
            if (value == null) return null;
            var encrypted = value.StartsWith("gcm:", StringComparison.Ordinal);
            var plain = encrypted ? encryption.Decrypt(value) : value;
            if (json && !string.IsNullOrWhiteSpace(plain))
            {
                using var document = JsonDocument.Parse(plain);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("Sensitive JSON must be an object; review legacy data before conversion.");
            }
            if (!encrypted)
            {
                var cipher = encryption.Encrypt(plain);
                changes.Add(() => set(cipher));
                verify.Add(() =>
                {
                    if (encryption.Decrypt(cipher) != plain) throw new CryptographicException("Conversion verification failed.");
                });
                fields++;
            }
            return plain;
        }

        foreach (var user in users)
        {
            var cccd = Protect(user.CCCD, v => user.CCCD = v);
            if (cccd != null)
            {
                var normalized = cccd.Trim();
                if (normalized.Length > 0 && !normalized.All(char.IsAsciiDigit))
                    throw new InvalidOperationException($"Invalid legacy CCCD on User ID {user.Id}; review before apply.");
                var hash = normalized.Length == 0 ? null : encryption.ComputeHmac("cccd:" + normalized);
                if (user.CccdLookupHash != hash)
                {
                    changes.Add(() => user.CccdLookupHash = hash);
                    hashes++;
                }
            }
            else if (user.CccdLookupHash != null)
            {
                changes.Add(() => user.CccdLookupHash = null);
                hashes++;
            }
            Protect(user.VerificationDocuments, v => user.VerificationDocuments = v, json: true);
            Protect(user.BankAccountMock, v => user.BankAccountMock = v, json: true);
        }
        foreach (var payout in payouts) Protect(payout.BankSnapshot, v => payout.BankSnapshot = v, json: true);
        foreach (var withdrawal in withdrawals)
        {
            Protect(withdrawal.BankName, v => withdrawal.BankName = v);
            Protect(withdrawal.BankAccountName, v => withdrawal.BankAccountName = v);
            Protect(withdrawal.BankAccountNumber, v => withdrawal.BankAccountNumber = v);
        }
        foreach (var log in sensitiveLogs)
        {
            if (log.EncryptionAlgorithm == "AES-256-GCM")
            {
                var plain = encryption.Decrypt(log.EncryptedData);
                if (!encryption.VerifyIntegrity(plain, log.DataHash, log.EncryptionAlgorithm))
                    throw new CryptographicException($"Sensitive log ID {log.Id} failed verification.");
                continue;
            }
            if (log.EncryptionAlgorithm != "AES-256-CBC")
                throw new CryptographicException($"Sensitive log ID {log.Id} has an unsupported algorithm.");
            var legacyPlain = encryption.DecryptLegacyLog(log.EncryptedData, log.DataHash);
            var cipher = encryption.Encrypt(legacyPlain);
            var hash = encryption.ComputeHmac(legacyPlain);
            changes.Add(() =>
            {
                log.EncryptedData = cipher;
                log.DataHash = hash;
                log.EncryptionAlgorithm = "AES-256-GCM";
                log.KeyIdentifier = "v1";
            });
            logs++;
        }

        if (apply)
        {
            foreach (var change in changes) change();
            foreach (var check in verify) check();
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();
            // Read database values again before committing, including hashes and authenticated tags.
            var after = await RunAsync(false, ct);
            if (after.NeedsConversion) throw new InvalidOperationException("Sensitive-data conversion verification failed.");
            await transaction!.CommitAsync(ct);
        }
        return new SensitiveDataMigrationResult(fields, logs, hashes);
    }
}
