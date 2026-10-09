namespace Northpad.Core.Vault;

public enum VaultProtection
{
    WindowsAccount = 1,
    Passphrase = 2,
}

public sealed class VaultLockedException : Exception
{
    public VaultLockedException()
        : base("The workspace is locked.")
    {
    }
}

public sealed class VaultUnlockFailedException : Exception
{
    public VaultUnlockFailedException(string message)
        : base(message)
    {
    }
}

public sealed class VaultStateException : Exception
{
    public VaultStateException(string message)
        : base(message)
    {
    }
}

public sealed class StorageException : Exception
{
    public StorageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
