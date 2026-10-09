using System.Security.Cryptography;

namespace Northpad.Core.Cryptography;

/// <summary>
/// AES-256-GCM using <see cref="AesGcm"/>. The payload layout is
/// version (1) || nonce (12) || tag (16) || ciphertext.
/// A new nonce is generated for every call. Callers must not reuse a key/nonce pair.
/// </summary>
public static class AesGcmCipher
{
    public static byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        if (key.Length != CryptoPolicy.KeySizeBytes)
        {
            throw new CryptographicException("The encryption key size is not valid.");
        }

        var nonce = RandomNumberGenerator.GetBytes(CryptoPolicy.NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[CryptoPolicy.TagSizeBytes];

        using (var aes = new AesGcm(key, CryptoPolicy.TagSizeBytes))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }

        var payload = new byte[1 + nonce.Length + tag.Length + ciphertext.Length];
        payload[0] = CryptoPolicy.PayloadVersion;
        nonce.CopyTo(payload.AsSpan(1));
        tag.CopyTo(payload.AsSpan(1 + nonce.Length));
        ciphertext.CopyTo(payload.AsSpan(1 + nonce.Length + tag.Length));
        return payload;
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> associatedData)
    {
        if (key.Length != CryptoPolicy.KeySizeBytes)
        {
            throw new CryptographicException("The encryption key size is not valid.");
        }

        const int header = 1 + CryptoPolicy.NonceSizeBytes + CryptoPolicy.TagSizeBytes;
        if (payload.Length < header || payload[0] != CryptoPolicy.PayloadVersion)
        {
            throw new CryptographicException("The encrypted payload is not recognized.");
        }

        var nonce = payload.Slice(1, CryptoPolicy.NonceSizeBytes);
        var tag = payload.Slice(1 + CryptoPolicy.NonceSizeBytes, CryptoPolicy.TagSizeBytes);
        var ciphertext = payload.Slice(header);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, CryptoPolicy.TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }
}
