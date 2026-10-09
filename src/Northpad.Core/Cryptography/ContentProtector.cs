using System.Security.Cryptography;
using System.Text;
using Northpad.Core.Vault;

namespace Northpad.Core.Cryptography;

public interface IContentProtector
{
    byte[] Protect(string plaintext, string context);

    string Unprotect(byte[] payload, string context);

    byte[] ProtectBytes(ReadOnlySpan<byte> plaintext, string context);

    byte[] UnprotectBytes(byte[] payload, string context);
}

public sealed class ContentProtector : IContentProtector
{
    private readonly VaultSession _session;

    public ContentProtector(VaultSession session)
    {
        _session = session;
    }

    public byte[] Protect(string plaintext, string context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            return AesGcmCipher.Encrypt(_session.DataKey, bytes, Encoding.UTF8.GetBytes(context));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public string Unprotect(byte[] payload, string context)
    {
        var bytes = AesGcmCipher.Decrypt(_session.DataKey, payload, Encoding.UTF8.GetBytes(context));
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public byte[] ProtectBytes(ReadOnlySpan<byte> plaintext, string context) =>
        AesGcmCipher.Encrypt(_session.DataKey, plaintext, Encoding.UTF8.GetBytes(context));

    public byte[] UnprotectBytes(byte[] payload, string context) =>
        AesGcmCipher.Decrypt(_session.DataKey, payload, Encoding.UTF8.GetBytes(context));
}
