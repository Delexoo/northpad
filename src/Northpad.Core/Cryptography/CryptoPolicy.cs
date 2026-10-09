namespace Northpad.Core.Cryptography;

public static class CryptoPolicy
{
    public const int KeySizeBytes = 32;
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;
    public const int SaltSizeBytes = 16;
    public const int MinimumPassphraseLength = 8;
    public const int MaximumPassphraseLength = 1024;

    /// <summary>
    /// OWASP Password Storage Cheat Sheet floor for PBKDF2-HMAC-SHA256.
    /// Stored with each passphrase wrap so a later KDF can be introduced
    /// without guessing which parameters produced an existing key.
    /// </summary>
    public const int DefaultPbkdf2Iterations = 600_000;

    public const byte PayloadVersion = 1;
}
