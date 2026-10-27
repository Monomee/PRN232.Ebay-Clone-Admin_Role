namespace EbayClone.Application.Common.Interfaces;

public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using AES-256-CBC with dynamic IV and returns Base64 ciphertext.
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
}
