using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Northpad.Core.Cryptography;
using Northpad.Core.Storage;

namespace Northpad.Core.Vault;

public interface IVaultService
{
    bool IsUnlocked { get; }

    VaultProtection Protection { get; }

    IContentProtector Protector { get; }

    void EnsureCreated();

    void UnlockWithWindowsAccount();

    void UnlockWithPassphrase(string passphrase);

    void UnlockWithRecoveryKey(string recoveryKey);

    string EnablePassphrase(string passphrase);

    void ChangePassphrase(string currentPassphrase, string newPassphrase);

    void DisablePassphrase(string currentPassphrase);

    string RotateRecoveryKey(string currentPassphrase);

    void Lock();

    void ExportBackup(string destinationDirectory);

    void DeleteAllData();
}

public sealed class VaultService : IVaultService, IDisposable
{
    private readonly AppPaths _paths;
    private readonly SqliteDatabase _database;
    private readonly ILogger<VaultService> _logger;
    private readonly VaultSession _session = new();
    private readonly int _iterations;

    public VaultService(AppPaths paths, SqliteDatabase database, ILogger<VaultService> logger)
        : this(paths, database, logger, CryptoPolicy.DefaultPbkdf2Iterations)
    {
    }

    internal VaultService(AppPaths paths, SqliteDatabase database, ILogger<VaultService> logger, int pbkdf2Iterations)
    {
        _paths = paths;
        _database = database;
        _logger = logger;
        _iterations = pbkdf2Iterations;
        Protector = new ContentProtector(_session);
    }

    public bool IsUnlocked => _session.IsUnlocked;

    public VaultProtection Protection { get; private set; } = VaultProtection.WindowsAccount;

    public IContentProtector Protector { get; }

    public void EnsureCreated()
    {
        _paths.EnsureDirectories();
        var databaseExists = File.Exists(_paths.DatabasePath);
        var keyExists = File.Exists(_paths.KeyPath);
        if (databaseExists && !keyExists)
        {
            throw new VaultStateException("The database exists but the vault key is missing. Northpad did not create a replacement key.");
        }

        if (!keyExists)
        {
            CreateWindowsAccountVault();
            _logger.LogInformation("Created a new local vault protected by the Windows account.");
        }

        _database.Migrate();
        Protection = KeyFile.Read(_paths.KeyPath).Protection;
    }

    public void UnlockWithWindowsAccount()
    {
        var file = KeyFile.Read(_paths.KeyPath);
        if (file.Protection != VaultProtection.WindowsAccount || file.DpapiBlob is null)
        {
            throw new VaultStateException("This workspace is protected by a passphrase.");
        }

        try
        {
            var key = DpapiProtector.Unprotect(file.DpapiBlob);
            _session.Unlock(key);
            _logger.LogInformation("Unlocked the vault with the Windows account.");
        }
        catch (CryptographicException)
        {
            throw new VaultStateException("Windows could not unwrap the vault key for this user account.");
        }
    }

    public void UnlockWithPassphrase(string passphrase)
    {
        ValidatePassphrase(passphrase);
        var file = RequirePassphraseFile();
        var kek = PassphraseKdf.Derive(passphrase, file.Salt!, file.Iterations);
        try
        {
            UnlockWrapped(kek, file.PassphraseWrappedDek!, FieldContext.PassphraseWrap, "That passphrase was not accepted.");
            _logger.LogInformation("Unlocked the vault with a passphrase.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public void UnlockWithRecoveryKey(string recoveryKey)
    {
        byte[] secret;
        try
        {
            secret = RecoveryKeyFormatter.Parse(recoveryKey);
        }
        catch (VaultUnlockFailedException)
        {
            throw;
        }

        try
        {
            var file = RequirePassphraseFile();
            UnlockWrapped(secret, file.RecoveryWrappedDek!, FieldContext.RecoveryWrap, "That recovery key was not accepted.");
            _logger.LogInformation("Unlocked the vault with a recovery key.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public string EnablePassphrase(string passphrase)
    {
        EnsureUnlocked();
        ValidatePassphrase(passphrase);
        if (Protection != VaultProtection.WindowsAccount)
        {
            throw new VaultStateException("A passphrase is already enabled.");
        }

        var recovery = RecoveryKeyFormatter.CreateSecret();
        try
        {
            var formatted = RecoveryKeyFormatter.Format(recovery);
            WritePassphraseFile(passphrase, recovery);
            Protection = VaultProtection.Passphrase;
            _logger.LogInformation("Enabled passphrase protection.");
            return formatted;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recovery);
        }
    }

    public void ChangePassphrase(string currentPassphrase, string newPassphrase)
    {
        EnsureUnlocked();
        ValidatePassphrase(newPassphrase);
        var file = RequirePassphraseFile();
        ConfirmPassphrase(file, currentPassphrase);
        var recoveryWrapped = file.RecoveryWrappedDek!;
        WritePassphraseWrap(newPassphrase, recoveryWrapped);
        _logger.LogInformation("Changed the vault passphrase.");
    }

    public void DisablePassphrase(string currentPassphrase)
    {
        EnsureUnlocked();
        var file = RequirePassphraseFile();
        ConfirmPassphrase(file, currentPassphrase);
        WriteWindowsAccountFile();
        Protection = VaultProtection.WindowsAccount;
        _logger.LogInformation("Removed passphrase protection. The Windows account protects the vault key.");
    }

    public string RotateRecoveryKey(string currentPassphrase)
    {
        EnsureUnlocked();
        var file = RequirePassphraseFile();
        ConfirmPassphrase(file, currentPassphrase);
        var recovery = RecoveryKeyFormatter.CreateSecret();
        try
        {
            var formatted = RecoveryKeyFormatter.Format(recovery);
            var passphraseWrapped = file.PassphraseWrappedDek!;
            var salt = file.Salt!;
            var recoveryWrapped = WrapDataKey(recovery, FieldContext.RecoveryWrap);
            KeyFile.Write(_paths.KeyPath, new KeyFile
            {
                Protection = VaultProtection.Passphrase,
                Salt = salt,
                Iterations = file.Iterations,
                PassphraseWrappedDek = passphraseWrapped,
                RecoveryWrappedDek = recoveryWrapped,
            });
            _logger.LogInformation("Rotated the recovery key.");
            return formatted;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recovery);
        }
    }

    public void Lock()
    {
        _session.Lock();
        _logger.LogInformation("Locked the vault.");
    }

    public void ExportBackup(string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        _database.Checkpoint();
        Directory.CreateDirectory(destinationDirectory);
        var databaseDestination = Path.Combine(destinationDirectory, "northpad.db");
        var keyDestination = Path.Combine(destinationDirectory, "vault.key");
        if (File.Exists(databaseDestination) || File.Exists(keyDestination))
        {
            throw new VaultStateException("The backup folder already contains Northpad files. Choose an empty folder.");
        }

        File.Copy(_paths.DatabasePath, databaseDestination);
        File.Copy(_paths.KeyPath, keyDestination);
        File.WriteAllText(Path.Combine(destinationDirectory, "README.txt"), BackupNotice(), Encoding.UTF8);
        _logger.LogInformation("Exported a local backup.");
    }

    public void DeleteAllData()
    {
        Lock();
        _database.ReleaseConnections();
        DeleteIfExists(_paths.DatabasePath);
        DeleteIfExists(_paths.DatabasePath + "-wal");
        DeleteIfExists(_paths.DatabasePath + "-shm");
        DeleteIfExists(_paths.KeyPath);
        DeleteIfExists(_paths.KeyPath + ".tmp");
        CreateWindowsAccountVault();
        _database.Migrate();
        Protection = VaultProtection.WindowsAccount;
        UnlockWithWindowsAccount();
        _logger.LogInformation("Deleted local workspace data and created an empty vault.");
    }

    public void Dispose() => _session.Dispose();

    private void CreateWindowsAccountVault()
    {
        var dek = RandomNumberGenerator.GetBytes(CryptoPolicy.KeySizeBytes);
        try
        {
            var blob = DpapiProtector.Protect(dek);
            KeyFile.Write(_paths.KeyPath, new KeyFile
            {
                Protection = VaultProtection.WindowsAccount,
                DpapiBlob = blob,
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    private void WriteWindowsAccountFile()
    {
        var copy = _session.DataKey.ToArray();
        try
        {
            var blob = DpapiProtector.Protect(copy);
            KeyFile.Write(_paths.KeyPath, new KeyFile
            {
                Protection = VaultProtection.WindowsAccount,
                DpapiBlob = blob,
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copy);
        }
    }

    private void WritePassphraseFile(string passphrase, byte[] recoverySecret)
    {
        var recoveryWrapped = WrapDataKey(recoverySecret, FieldContext.RecoveryWrap);
        WritePassphraseWrap(passphrase, recoveryWrapped);
    }

    private void WritePassphraseWrap(string passphrase, byte[] recoveryWrapped)
    {
        var salt = RandomNumberGenerator.GetBytes(CryptoPolicy.SaltSizeBytes);
        var kek = PassphraseKdf.Derive(passphrase, salt, _iterations);
        try
        {
            var wrapped = WrapDataKey(kek, FieldContext.PassphraseWrap);
            KeyFile.Write(_paths.KeyPath, new KeyFile
            {
                Protection = VaultProtection.Passphrase,
                Salt = salt,
                Iterations = _iterations,
                PassphraseWrappedDek = wrapped,
                RecoveryWrappedDek = recoveryWrapped,
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private byte[] WrapDataKey(ReadOnlySpan<byte> wrappingKey, ReadOnlySpan<byte> associatedData)
    {
        var copy = _session.DataKey.ToArray();
        try
        {
            return AesGcmCipher.Encrypt(wrappingKey, copy, associatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copy);
        }
    }

    private void ConfirmPassphrase(KeyFile file, string passphrase)
    {
        ValidatePassphrase(passphrase);
        var kek = PassphraseKdf.Derive(passphrase, file.Salt!, file.Iterations);
        try
        {
            byte[] unwrapped;
            try
            {
                unwrapped = AesGcmCipher.Decrypt(kek, file.PassphraseWrappedDek!, FieldContext.PassphraseWrap);
            }
            catch (CryptographicException)
            {
                throw new VaultUnlockFailedException("That passphrase was not accepted.");
            }

            try
            {
                if (!CryptographicOperations.FixedTimeEquals(unwrapped, _session.DataKey))
                {
                    throw new VaultStateException("The vault key does not match this workspace.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(unwrapped);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private void UnlockWrapped(byte[] wrappingKey, byte[] wrapped, ReadOnlySpan<byte> associatedData, string failureMessage)
    {
        try
        {
            var key = AesGcmCipher.Decrypt(wrappingKey, wrapped, associatedData);
            _session.Unlock(key);
        }
        catch (CryptographicException)
        {
            throw new VaultUnlockFailedException(failureMessage);
        }
    }

    private KeyFile RequirePassphraseFile()
    {
        var file = KeyFile.Read(_paths.KeyPath);
        if (file.Protection != VaultProtection.Passphrase ||
            file.Salt is null ||
            file.PassphraseWrappedDek is null ||
            file.RecoveryWrappedDek is null)
        {
            throw new VaultStateException("This workspace is not protected by a passphrase.");
        }

        return file;
    }

    private void EnsureUnlocked()
    {
        if (!_session.IsUnlocked)
        {
            throw new VaultLockedException();
        }
    }

    private static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < CryptoPolicy.MinimumPassphraseLength)
        {
            throw new VaultStateException($"Use a passphrase of at least {CryptoPolicy.MinimumPassphraseLength} characters.");
        }

        if (passphrase.Length > CryptoPolicy.MaximumPassphraseLength)
        {
            throw new VaultStateException($"Use a passphrase of {CryptoPolicy.MaximumPassphraseLength} characters or fewer.");
        }
    }

    private string BackupNotice()
    {
        var protection = Protection == VaultProtection.Passphrase
            ? "This backup is protected by the Northpad passphrase and recovery key. The Windows account alone cannot unwrap it."
            : "This backup is protected by Windows DPAPI for the user account that created it. Copying it to another Windows user or another computer will not make it readable.";

        return """
            Northpad backup
            ===============

            This folder contains a copy of the local database and the wrapped vault key.
            It does not contain the unwrapped data key.

            """ + protection + """


            Northpad does not upload this backup. Restore it only by replacing both
            northpad.db and vault.key together, while Northpad is closed, in the
            local Northpad data folder. Restoring one file without the other makes
            the workspace unreadable. Northpad will not invent a new key for an
            existing database.
            """;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
