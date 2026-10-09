using System.Security.Cryptography;
using System.Text;
using Northpad.Core.Cryptography;

namespace Northpad.Core.Tests;

public class CryptoTests
{
    [Fact]
    public void AesGcm_roundtrips_unicode_and_empty_text()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var aad = "northpad:test"u8.ToArray();
        foreach (var text in new[] { "", "café 筆記", new string('a', 100_000) })
        {
            var payload = AesGcmCipher.Encrypt(key, Encoding.UTF8.GetBytes(text), aad);
            var plain = AesGcmCipher.Decrypt(key, payload, aad);
            Assert.Equal(text, Encoding.UTF8.GetString(plain));
        }
    }

    [Fact]
    public void AesGcm_uses_a_fresh_nonce()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plain = "same"u8.ToArray();
        var aad = "ctx"u8.ToArray();
        var first = AesGcmCipher.Encrypt(key, plain, aad);
        var second = AesGcmCipher.Encrypt(key, plain, aad);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Tampered_ciphertext_fails_authentication()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var payload = AesGcmCipher.Encrypt(key, "secret note"u8.ToArray(), "aad"u8.ToArray());
        payload[^1] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, payload, "aad"u8.ToArray()));
    }

    [Fact]
    public void Tampered_tag_fails_authentication()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var payload = AesGcmCipher.Encrypt(key, "secret note"u8.ToArray(), "aad"u8.ToArray());
        payload[1 + 12] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, payload, "aad"u8.ToArray()));
    }

    [Fact]
    public void Wrong_associated_data_fails_authentication()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var payload = AesGcmCipher.Encrypt(key, "secret note"u8.ToArray(), "title"u8.ToArray());
        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, payload, "body"u8.ToArray()));
    }

    [Fact]
    public void Pbkdf2_is_deterministic_and_password_sensitive()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var first = PassphraseKdf.Derive("correct horse", salt, 1_000);
        var second = PassphraseKdf.Derive("correct horse", salt, 1_000);
        var wrong = PassphraseKdf.Derive("correct horses", salt, 1_000);
        Assert.Equal(first, second);
        Assert.NotEqual(first, wrong);
        Assert.Equal(32, first.Length);
    }

    [Fact]
    public void Default_iteration_count_meets_the_owasp_floor()
    {
        Assert.True(CryptoPolicy.DefaultPbkdf2Iterations >= 600_000);
    }
}
