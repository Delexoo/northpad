# Security

Northpad 0.1.0 encrypts note and task text at rest and wraps the data key with DPAPI or a passphrase. That is the security behavior this version implements. It is not a claim that the workspace is hidden from Windows, from malware running as the same user, or from a person who can read the process while it is unlocked.

## Cryptography in use

All of these come from the .NET 10 base class library or the Windows DPAPI wrapper in `System.Security.Cryptography.ProtectedData` 10.0.12. Northpad does not ship a custom cipher.

| Purpose | Implementation |
| --- | --- |
| Content encryption | AES-256-GCM, `AesGcm`, 12-byte random nonce, 16-byte tag |
| Data key | 32 bytes from `RandomNumberGenerator` |
| Passphrase key derivation | PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt, `Rfc2898DeriveBytes.Pbkdf2` |
| Windows-account wrap | `ProtectedData` with `DataProtectionScope.CurrentUser` |
| Recovery wrap | AES-256-GCM under a random 256-bit recovery key |
| Comparison of unwrapped keys | `CryptographicOperations.FixedTimeEquals` |

Associated data binds a ciphertext to its record and field. Tampering or swapping blobs fails decryption. The .NET 8+ behavior applies: a tag mismatch throws `AuthenticationTagMismatchException` and clears the destination buffer.

PBKDF2 was chosen over a third-party Argon2 package. OWASP's Password Storage Cheat Sheet prefers Argon2id for new password storage. The Argon2 libraries reviewed on 9 October 2026 were Konscious.Security.Cryptography.Argon2 1.3.1 (last updated 19 June 2024) and Isopoh.Cryptography.Argon2 2.0.0 (Creative Commons Attribution). The iteration count is stored in `vault.key`, so a later version can add Argon2id without guessing how existing keys were derived. This version does not claim Argon2.

The passphrase must be 8 to 1024 characters. Northpad does not impose composition rules. A short passphrase can still be guessed; 600,000 iterations slows guessing, it does not make a weak passphrase strong.

## Locking

With only DPAPI, there is no lock screen. A lock screen that unlocked itself through DPAPI would not be a security control against another program running as you. After a passphrase is set, startup shows the lock screen, Lock in Settings zeroes the in-memory data key, and the DPAPI wrap is gone.

Passphrases and recovery keys are .NET strings for as long as the UI and GC retain them. Northpad zeroes the byte arrays it allocates for key derivation. It cannot wipe the immutable passphrase string.

The data key is pinned for the session and zeroed on lock. That reduces but does not eliminate copies made by the garbage collector, crash dumps, or a debugger.

## What this version does not do

- End-to-end encryption. There is no remote party.
- Encryption of task completion, priority, due date, or timestamps.
- Protection from malware or a debugger running as the same Windows user while the vault is unlocked.
- Protection from a compromised operating system.
- Windows Hello.
- A plaintext-free hibernation or pagefile story. Windows manages those.
- Guaranteed secure deletion of old ciphertext on SSDs.
- Certificate pinning, token storage, or account recovery. There is no account.

## Network

Network tools are off until Settings allows them. Search then uses the SearXNG address you set (searx.be by default). Maps request tiles from tile.openstreetmap.org and, if you search for a place, from Nominatim. YouTube opens the Piped frontend; playback can also request video files from YouTube's servers. The browser uses Windows WebView2 and opens only addresses you enter. Translate sends text only to a LibreTranslate address you type, and it refuses Google, Microsoft, Apple, and DeepL hosts. There is no telemetry and no AI service.

## Files you should not publish

Do not attach `vault.key`, `northpad.db`, backups, or log files to a bug report. Logs are written to avoid note text, but a report does not need them in order to describe a crash.

## Reporting a vulnerability

There is no hosted bug tracker in this repository yet. Report privately to the maintainer of your copy. Include the Northpad version and what you observed. Do not include vault files, recovery keys, or note contents.

If a report describes a failure of the cryptography above, treat it as a defect in this design rather than as a reason to invent a replacement algorithm inside the app.
