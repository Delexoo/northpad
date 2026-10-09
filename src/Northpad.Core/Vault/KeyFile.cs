using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Northpad.Core.Cryptography;

namespace Northpad.Core.Vault;

internal sealed class KeyFile
{
    private static ReadOnlySpan<byte> Magic => "NPKEY1"u8;

    public required VaultProtection Protection { get; init; }

    public byte[]? DpapiBlob { get; init; }

    public byte[]? Salt { get; init; }

    public int Iterations { get; init; }

    public byte[]? PassphraseWrappedDek { get; init; }

    public byte[]? RecoveryWrappedDek { get; init; }

    public static KeyFile Read(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VaultStateException("The vault key file could not be read. Northpad did not create a replacement key.");
        }

        if (bytes.Length < Magic.Length + 2 || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
        }

        var offset = Magic.Length;
        var format = bytes[offset++];
        if (format != 1)
        {
            throw new VaultStateException("The vault key file was written by a newer version of Northpad.");
        }

        var mode = bytes[offset++];
        if (mode == (byte)VaultProtection.WindowsAccount)
        {
            var blob = ReadBlob(bytes, ref offset);
            if (offset != bytes.Length)
            {
                throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
            }

            return new KeyFile
            {
                Protection = VaultProtection.WindowsAccount,
                DpapiBlob = blob,
            };
        }

        if (mode == (byte)VaultProtection.Passphrase)
        {
            if (bytes.Length < offset + CryptoPolicy.SaltSizeBytes + sizeof(int))
            {
                throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
            }

            var salt = bytes.AsSpan(offset, CryptoPolicy.SaltSizeBytes).ToArray();
            offset += CryptoPolicy.SaltSizeBytes;
            var iterations = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
            offset += sizeof(int);
            if (iterations < 1)
            {
                throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
            }

            var passphraseWrap = ReadBlob(bytes, ref offset);
            var recoveryWrap = ReadBlob(bytes, ref offset);
            if (offset != bytes.Length)
            {
                throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
            }

            return new KeyFile
            {
                Protection = VaultProtection.Passphrase,
                Salt = salt,
                Iterations = iterations,
                PassphraseWrappedDek = passphraseWrap,
                RecoveryWrappedDek = recoveryWrap,
            };
        }

        throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
    }

    public static void Write(string path, KeyFile file)
    {
        using var stream = new MemoryStream();
        stream.Write(Magic);
        stream.WriteByte(1);
        stream.WriteByte((byte)file.Protection);

        if (file.Protection == VaultProtection.WindowsAccount)
        {
            WriteBlob(stream, file.DpapiBlob ?? throw new VaultStateException("The vault key could not be stored."));
        }
        else if (file.Protection == VaultProtection.Passphrase)
        {
            var salt = file.Salt ?? throw new VaultStateException("The vault key could not be stored.");
            if (salt.Length != CryptoPolicy.SaltSizeBytes || file.Iterations < 1)
            {
                throw new VaultStateException("The vault key could not be stored.");
            }

            stream.Write(salt);
            Span<byte> iterations = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(iterations, file.Iterations);
            stream.Write(iterations);
            WriteBlob(stream, file.PassphraseWrappedDek ?? throw new VaultStateException("The vault key could not be stored."));
            WriteBlob(stream, file.RecoveryWrappedDek ?? throw new VaultStateException("The vault key could not be stored."));
        }
        else
        {
            throw new VaultStateException("The vault key could not be stored.");
        }

        AtomicFile.WriteAllBytes(path, stream.ToArray());
    }

    private static byte[] ReadBlob(byte[] bytes, ref int offset)
    {
        if (bytes.Length < offset + sizeof(int))
        {
            throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
        offset += sizeof(int);
        if (length <= 0 || length > 1_048_576 || bytes.Length < offset + length)
        {
            throw new VaultStateException("The vault key file is not recognized. Northpad did not create a replacement key.");
        }

        var blob = bytes.AsSpan(offset, length).ToArray();
        offset += length;
        return blob;
    }

    private static void WriteBlob(Stream stream, byte[] blob)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, blob.Length);
        stream.Write(length);
        stream.Write(blob);
    }
}

internal static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, content);
        if (File.Exists(path))
        {
            File.Replace(temporary, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporary, path);
        }
    }
}

internal static class DpapiProtector
{
    private static ReadOnlySpan<byte> Entropy => "northpad-vault-dek-v1"u8;

    public static byte[] Protect(byte[] data)
    {
        return ProtectedData.Protect(data, Entropy.ToArray(), DataProtectionScope.CurrentUser);
    }

    public static byte[] Unprotect(byte[] data)
    {
        return ProtectedData.Unprotect(data, Entropy.ToArray(), DataProtectionScope.CurrentUser);
    }
}
