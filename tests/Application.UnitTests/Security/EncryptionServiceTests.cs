using System.Security.Cryptography;
using System.Text;
using EbayClone.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class EncryptionServiceTests
{
    internal static (string Cipher, string Hash) Legacy(string plain, string secret = "test-legacy-only")
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(plain)).ToArray();
        var cipher = aes.IV.Concat(aes.EncryptCbc(bytes, aes.IV)).ToArray();
        return (Convert.ToBase64String(cipher), Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(plain))));
    }

    [Test]
    public void Legacy_RequiresExplicitKey_AndValidHmac()
    {
        var (cipher, hash) = Legacy("Dữ liệu cũ");
        Assert.That(Create(legacy: "test-legacy-only").DecryptLegacyLog(cipher, hash), Is.EqualTo("Dữ liệu cũ"));
        Assert.Catch<CryptographicException>(() => Create().DecryptLegacyLog(cipher, hash));
        Assert.Catch<CryptographicException>(() => Create(legacy: "wrong").DecryptLegacyLog(cipher, hash));
        Assert.Catch<CryptographicException>(() => Create(legacy: "test-legacy-only").DecryptLegacyLog(cipher, new string('0', 64)));
        Assert.Catch<CryptographicException>(() => Create(legacy: "test-legacy-only").DecryptLegacyLog(cipher, null));
    }

    internal static AesEncryptionService Create(byte keyByte = 7, string? legacy = null) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:EncryptionKey"] = Convert.ToBase64String(Enumerable.Repeat(keyByte, 32).ToArray()),
            ["Security:LegacyEncryptionKey"] = legacy
        }).Build());

    [TestCase("")]
    [TestCase("{\"cccd\":\"001234567890\",\"name\":\"Nguyễn Văn An\"}")]
    public void RoundTrip_UsesAuthenticatedRandomEncryption(string plain)
    {
        var service = Create();
        var first = service.Encrypt(plain);
        Assert.That(first, Does.StartWith("gcm:v1:"));
        Assert.That(first, Is.Not.EqualTo(service.Encrypt(plain)));
        Assert.That(Create().Decrypt(first), Is.EqualTo(plain));
    }

    [Test]
    public void WrongKey_AndTamperedPayload_AreRejected()
    {
        var service = Create();
        var cipher = service.Encrypt("sensitive");
        Assert.Catch<CryptographicException>(() => Create(8).Decrypt(cipher));
        foreach (var index in new[] { 0, 12, 28 })
        {
            var bytes = Convert.FromBase64String(cipher[7..]);
            bytes[index] ^= 1;
            Assert.Catch<CryptographicException>(() => service.Decrypt("gcm:v1:" + Convert.ToBase64String(bytes)));
        }
    }

    [TestCase(null)]
    [TestCase("not-base64")]
    [TestCase("c2hvcnQ=")]
    public void MissingOrInvalidKey_FailsClosed(string? key)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Security:EncryptionKey"] = key }).Build();
        Assert.Throws<InvalidOperationException>(() => new AesEncryptionService(config));
    }

    [TestCase("plaintext")]
    [TestCase("gcm:v2:AAAA")]
    [TestCase("gcm:v1:AAAA")]
    public void MalformedCipher_DoesNotReturnErrorAsPlaintext(string cipher) =>
        Assert.Catch<CryptographicException>(() => Create().Decrypt(cipher));

    [Test]
    public void Hmac_IsStableAcrossInstances_AndSeparatedFromEncryptionKey()
    {
        var hash = Create().ComputeHmac("cccd:001234567890");
        Assert.That(hash, Has.Length.EqualTo(64));
        Assert.That(Create().ComputeHmac("cccd:001234567890"), Is.EqualTo(hash));
        Assert.That(Create(8).ComputeHmac("cccd:001234567890"), Is.Not.EqualTo(hash));
        Assert.That(Create().ComputeHmac("cccd:1234567890"), Is.Not.EqualTo(hash));
        var direct = HMACSHA256.HashData(Enumerable.Repeat((byte)7, 32).ToArray(), Encoding.UTF8.GetBytes("cccd:001234567890"));
        Assert.That(hash, Is.Not.EqualTo(Convert.ToHexString(direct).ToLowerInvariant()));
    }
}
