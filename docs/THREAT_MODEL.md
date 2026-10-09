# Threat model

Scope: Northpad 0.1.0 on Windows, as implemented. This is a local notes and tasks workspace with optional passphrase locking. It is not a messaging system, a password manager, or a synchronized store.

## Assets

- Note title and body
- Task title and details
- The 256-bit data key
- The passphrase and recovery key, when they exist
- Metadata that is stored in plaintext: timestamps, completion, priority, due date, theme, record ids
- The fact that Northpad is installed and how large the database is

## Actors

| Actor | Capability assumed |
| --- | --- |
| User | Uses the Windows account that owns the vault |
| Another local Windows user | Separate account, no admin rights over the user's profile |
| Thief with the locked disk or a copied backup | Has files, does not have the Windows logon secret or the passphrase |
| Malware running as the user | Can read the user's files, call DPAPI, debug the process, or capture keystrokes |
| Compromised operating system or administrator | Can read memory, disk, and input |
| Future sync server | Not in this version. Listed so the boundary stays explicit |

## What the current design resists

**Another Windows user on the same PC, without administrator access.** The database and key live in the user's local profile. The data key is wrapped with DPAPI current-user scope, or with a passphrase. Another standard user does not get the DPAPI master key and does not get the passphrase.

**A copied database and key file taken to another computer,** when protection is still DPAPI. Microsoft documents that DPAPI decryption normally requires the same user on the same computer. A passphrase-protected copy is readable only with the passphrase or the recovery key, which are not in the file.

**Tampering with ciphertext.** AES-GCM authentication fails closed. Tests cover flipped ciphertext, a flipped tag, wrong associated data, and a tampered note blob in SQLite. A wrong passphrase does not rewrite `vault.key`.

**Accidental plaintext of note and task text in the database files.** A test writes a distinctive string and checks that it is absent from the files in the vault directory after a WAL checkpoint.

**A failed SQL transaction destroying unrelated rows.** The failed-transaction test rolls back an uncommitted insert.

**Replacing a lost key with a new one and pretending the old database is empty but healthy.** If the database exists and the key file does not, startup throws and does not create a key.

## What the current design does not resist

**Malware or another process running as the same user while Northpad is unlocked.** It can read the data key from process memory, or read decrypted text from the UI. DPAPI does not stop this, because the malware can call `CryptUnprotectData` as the user. A passphrase stops that only while the vault is locked and the passphrase has not been captured.

**Malware running as the same user while the vault is DPAPI-only.** There is no lock screen in that mode on purpose. The malware can unwrap the key.

**A compromised operating system, kernel driver, or administrator.** Northpad runs inside that system. Encryption at rest does not survive a hostile OS.

**Someone who watches the screen or records the keyboard.** A passphrase can be captured while it is typed. The recovery key can be captured when it is displayed.

**Cold-boot, hibernation, pagefile, and crash-dump attacks.** The process zeroes the key buffer it owns. Windows may already have copied pages elsewhere. This version does not configure crash dumps or BitLocker. Disk encryption, if the user turns it on in Windows, is outside the app and is the right control for a stolen powered-off laptop.

**Secure deletion.** Deleting a note removes the SQLite row. WAL checkpoints and SSD wear leveling can leave old pages. Northpad does not claim that deleted text is gone from the physical media.

**Metadata.** An attacker who can read the database can see how many notes and tasks exist, when they were created and edited, which tasks are done, their priority, and their due dates. They can also see ciphertext lengths.

**A malicious imported file.** This version does not import files. When Files is built, that threat has to be handled before the feature is exposed. Notes and Todo are plain text in WPF text boxes, not HTML.

**A compromised sync server, stolen account password, or stolen refresh token.** There is no server and no account. Those attacks do not apply until that feature exists. The intended rule, recorded in the roadmap, is that a server must not be able to decrypt content.

**Dependency compromise after this commit.** The NuGet audit on 9 October 2026 reported no known vulnerabilities in the resolved graph. That check goes stale.

## Locked versus unlocked

| State | Data key | Who can read note text from disk |
| --- | --- | --- |
| DPAPI mode, app running | In process memory | This Windows user, including other programs they run |
| DPAPI mode, app closed | Only the DPAPI wrap is on disk | This Windows user, once they unwrap it |
| Passphrase mode, locked | Not in the process | Someone with the passphrase or recovery key |
| Passphrase mode, unlocked | In process memory | The same as DPAPI mode while unlocked |

## Decisions that follow from this model

- Do not show a lock button that only hides the window under DPAPI.
- Do not keep a DPAPI copy of the key after a passphrase is set.
- Do not send content to a server in a later account feature unless the user opts in, and do not give the server the data key.
- Do not describe Maps or Messages as private until they exist and their network behavior is explicit.
- Do not store payment-card numbers or account passwords in the task and note tables as a stand-in for a wallet. That feature needs its own design.
