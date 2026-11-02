using System.Security.Cryptography;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EbayClone.Infrastructure.Services;

public sealed class AesEncryptionService : IEncryptionService
{
    private const string Prefix = "gcm:v1:";
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes(Prefix);
    private readonly byte[] _key;
    private readonly byte[] _hmacKey;
    private readonly byte[]? _legacyKey;

    public AesEncryptionService(IConfiguration configuration)
    {
        try
        {
            _key = Convert.FromBase64String(configuration["Security:EncryptionKey"] ?? "");
            if (_key.Length != 32) throw new FormatException();
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Security:EncryptionKey must be a persistent Base64-encoded 32-byte key.");
        }
        _hmacKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, _key, 32,
            info: Encoding.UTF8.GetBytes("ebay-sensitive-data:hmac:v1"));
        var legacy = configuration["Security:LegacyEncryptionKey"];
        if (!string.IsNullOrEmpty(legacy)) _legacyKey = SHA256.HashData(Encoding.UTF8.GetBytes(legacy));
    }

    public string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var buffer = new byte[28 + plain.Length];
        RandomNumberGenerator.Fill(buffer.AsSpan(0, 12));
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(buffer.AsSpan(0, 12), plain, buffer.AsSpan(28), buffer.AsSpan(12, 16), AssociatedData);
        return Prefix + Convert.ToBase64String(buffer);
    }

    public string Decrypt(string cipherText)
    {
        try
        {
            if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal))
                throw new CryptographicException("Unsupported sensitive-data format. Run the data migration first.");
            var buffer = Convert.FromBase64String(cipherText[Prefix.Length..]);
            if (buffer.Length < 28) throw new CryptographicException("Invalid sensitive-data payload.");
            var plain = new byte[buffer.Length - 28];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(buffer.AsSpan(0, 12), buffer.AsSpan(28), buffer.AsSpan(12, 16), plain, AssociatedData);
            return Encoding.UTF8.GetString(plain);
        }
        catch (FormatException) { throw new CryptographicException("Invalid sensitive-data payload."); }
    }

    public string ComputeHmac(string data) =>
        Convert.ToHexString(HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    public bool VerifyIntegrity(string plainText, string? hash, string algorithm)
    {
        var key = algorithm switch
        {
            "AES-256-GCM" => _hmacKey,
            "AES-256-CBC" => _legacyKey,
            _ => null
        };
        if (key == null || hash == null) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash),
                HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(plainText)));
        }
        catch (FormatException) { return false; }
    }

    public string DecryptLegacyLog(string cipherText, string? hash)
    {
        if (_legacyKey == null) throw new CryptographicException("Security:LegacyEncryptionKey is required to convert CBC logs.");
        try
        {
            var buffer = Convert.FromBase64String(cipherText);
            if (buffer.Length < 32 || buffer.Length % 16 != 0) throw new CryptographicException("Invalid legacy log.");
            using var aes = Aes.Create();
            aes.Key = _legacyKey;
            var bytes = aes.DecryptCbc(buffer.AsSpan(16), buffer.AsSpan(0, 16));
            // The old StreamWriter emitted a UTF-8 BOM; StreamReader removed it before hashing.
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
            var plain = reader.ReadToEnd();
            if (!VerifyIntegrity(plain, hash, "AES-256-CBC")) throw new CryptographicException("Legacy log integrity verification failed.");
            return plain;
        }
        catch (FormatException) { throw new CryptographicException("Invalid legacy log."); }
    }
}
