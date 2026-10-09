using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Northpad.Core.Vault;

public sealed class VaultSession : IDisposable
{
    private byte[]? _dataKey;
    private GCHandle _pin;
    private bool _disposed;

    public bool IsUnlocked => _dataKey is not null;

    internal ReadOnlySpan<byte> DataKey
    {
        get
        {
            if (_dataKey is null)
            {
                throw new VaultLockedException();
            }

            return _dataKey;
        }
    }

    internal void Unlock(byte[] dataKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (dataKey.Length != Cryptography.CryptoPolicy.KeySizeBytes)
        {
            throw new CryptographicException("The encryption key size is not valid.");
        }

        Lock();
        _dataKey = dataKey;
        _pin = GCHandle.Alloc(_dataKey, GCHandleType.Pinned);
    }

    public void Lock()
    {
        if (_dataKey is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_dataKey);
        if (_pin.IsAllocated)
        {
            _pin.Free();
        }

        _dataKey = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Lock();
        _disposed = true;
    }
}
