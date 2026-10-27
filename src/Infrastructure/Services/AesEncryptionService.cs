using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EbayClone.Infrastructure.Services;

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        // Lấy secret key từ cấu hình (Security:EncryptionKey) hoặc fallback chuẩn xác
        var secret = configuration["Security:EncryptionKey"] 
                     ?? "DefaultSuperSecretKeyForEbayClone2026AES256Encryption!";

        // Dùng SHA-256 để luôn đảm bảo khóa có độ dài chính xác 256-bit (32 bytes)
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // Tạo IV ngẫu nhiên cho mỗi lần mã hóa (16 bytes)

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // Ghi IV vào đầu stream để khi giải mã lấy lại được IV
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            // Đọc 16 bytes đầu tiên làm IV
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);

            return sr.ReadToEnd();
        }
        catch
        {
            // Nếu giải mã lỗi, trả về thông báo lỗi an toàn
            return "[DECRYPTION_ERROR: Invalid key or corrupted payload]";
        }
    }

    public string ComputeHmac(string data)
    {
        if (string.IsNullOrEmpty(data)) return string.Empty;

        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
