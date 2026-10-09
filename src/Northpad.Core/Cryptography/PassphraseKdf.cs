using System.Security.Cryptography;
using System.Text;

namespace Northpad.Core.Cryptography;

public static class PassphraseKdf
{
    public static byte[] Derive(string passphrase, ReadOnlySpan<byte> salt, int iterations)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        if (salt.Length != CryptoPolicy.SaltSizeBytes)
        {
            throw new CryptographicException("The salt size is not valid.");
        }

        if (iterations < 1)
        {
            throw new CryptographicException("The iteration count is not valid.");
        }

        var passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                CryptoPolicy.KeySizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
