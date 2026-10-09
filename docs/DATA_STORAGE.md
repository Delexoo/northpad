# Data storage

## Location

Default root:

`%LOCALAPPDATA%\Northpad`

| File | Role |
| --- | --- |
| `northpad.db` | SQLite database |
| `northpad.db-wal`, `northpad.db-shm` | WAL mode companions while the database is open |
| `vault.key` | Wrapped data key. Not the unwrapped key |
| `logs\northpad.log` | Operational log. Rotates to `northpad.log.1` near 1 MB |

Settings shows this path and can open the folder. Export copies `northpad.db` and `vault.key` into a new `NorthpadBackup-yyyyMMdd-HHmmss` folder plus a `README.txt`. Export does not upload anything. Restore is manual: close Northpad and replace both files together. Replacing only one of them makes the workspace unreadable. If the database exists and `vault.key` does not, Northpad stops and does not create a replacement key.

## Database

Engine: SQLite 3.53.3, bundled by SQLitePCLRaw 2.1.12 through Microsoft.Data.Sqlite 10.0.12. Measured by `SELECT sqlite_version()` on 9 October 2026.

Pragmas on each open: `foreign_keys=ON`, `journal_mode=WAL`, `synchronous=FULL`, `busy_timeout=5000`.

Schema version lives in `schema_info`. Version 1 creates `settings`, `notes`, and `tasks`. Migrations run inside a transaction. A newer version number aborts without modifying the file.

The database file is not encrypted by SQLCipher. Microsoft.Data.Sqlite's default native library does not implement database encryption. Sensitive text is encrypted by the application before it is stored in BLOB columns.

## What is encrypted

AES-256-GCM payload layout:

`version (1 byte) || nonce (12) || tag (16) || ciphertext`

Associated data is `northpad:<record>:<field>:v1:<guid>`. A blob moved to another row or column fails authentication.

| Data | At rest |
| --- | --- |
| Note title, note body | Encrypted |
| Task title, task details | Encrypted |
| Note and task ids | Plaintext random GUIDs |
| Created and updated timestamps | Plaintext ISO-8601 UTC |
| Task completed flag, priority, due date | Plaintext |
| Theme setting | Plaintext |
| Wrapped data key | DPAPI blob, or passphrase wrap plus recovery wrap |

Ciphertext length tracks plaintext length. Northpad does not pad fields in this version.

Search decrypts notes and tasks in memory while the vault is unlocked. There is no plaintext search index on disk.

## Key file

`vault.key` starts with the ASCII magic `NPKEY1`, a format version byte, and a mode byte.

Windows-account mode stores a DPAPI blob from `ProtectedData.Protect` with `DataProtectionScope.CurrentUser` and the non-secret entropy string `northpad-vault-dek-v1`. DPAPI ties the blob to this Windows user on this computer. Copying the folder to another user or another PC does not make it readable, except for the roaming-profile cases Microsoft documents for DPAPI.

Passphrase mode stores:

- 16-byte salt
- PBKDF2-HMAC-SHA256 iteration count (600,000 for keys created by this version)
- AES-256-GCM wrap of the data key under the derived key
- AES-256-GCM wrap of the same data key under a random 256-bit recovery key

The passphrase and the recovery key are not stored. A wrong passphrase fails authentication and does not rewrite the file. The recovery key is displayed as uppercase hex in groups of four. It is shown when passphrase protection is enabled and when the recovery key is rotated.

## Writes and crash behavior

Repository writes run inside a SQLite transaction. A failed transaction rolls back. WAL plus `synchronous=FULL` is the crash-recovery mode described by SQLite for WAL databases. Notes and Todo also debounce editor writes (about 400 ms and 300 ms) and flush when the module closes or the window closes. Text typed after the last successful flush can be lost if the process is killed.

## Delete and export

Delete note and delete task remove that row. "Delete local data" in Settings closes the database, deletes the database, WAL, shared-memory, and key files, then creates a new Windows-account vault. It does not delete backup folders you already exported.

Backups are only as protected as the key file inside them. A passphrase backup moves with the passphrase and recovery key. A Windows-account backup remains bound to that Windows user.

## Logs and temporary files

The logger is not given note bodies, task text, passphrases, or keys. SQLite may keep WAL and shared-memory files beside the database while it is open. Northpad does not copy user text into `%TEMP%` in this version. Secure deletion of leftover flash pages is not possible for this application to guarantee; see the threat model.
