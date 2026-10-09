using System.Globalization;
using System.Security.Cryptography;

namespace Northpad.Core.Vault;

public static class RecoveryKeyFormatter
{
    public static byte[] CreateSecret() => RandomNumberGenerator.GetBytes(CryptoPolicyKeySize());

    public static string Format(ReadOnlySpan<byte> secret)
    {
        var hex = Convert.ToHexString(secret);
        var groups = new string[hex.Length / 4];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = hex.Substring(i * 4, 4);
        }

        return string.Join(' ', groups);
    }

    public static byte[] Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new VaultUnlockFailedException("The recovery key format was not recognized.");
        }

        var compact = new char[text.Length];
        var length = 0;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || character is '-' or ':')
            {
                continue;
            }

            compact[length++] = character;
        }

        var hex = new string(compact, 0, length);
        if (hex.Length != 64 || !hex.All(Uri.IsHexDigit))
        {
            throw new VaultUnlockFailedException("The recovery key format was not recognized.");
        }

        return Convert.FromHexString(hex);
    }

    private static int CryptoPolicyKeySize() => Cryptography.CryptoPolicy.KeySizeBytes;
}
