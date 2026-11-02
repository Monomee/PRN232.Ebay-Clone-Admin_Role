namespace EbayClone.Application.Common.Interfaces;

public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext with authenticated AES-256-GCM and a versioned envelope.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts Base64 ciphertext back to plaintext.
    /// </summary>
    string Decrypt(string cipherText);

    /// <summary>
    /// Computes HMAC-SHA256 hash for data integrity verification.
    /// </summary>
    string ComputeHmac(string data);

    bool VerifyIntegrity(string plainText, string? hash, string algorithm);

    /// <summary>Reads CBC logs only when their legacy HMAC is valid.</summary>
    string DecryptLegacyLog(string cipherText, string? hash);
}
