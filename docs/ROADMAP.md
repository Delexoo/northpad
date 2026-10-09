# Roadmap

Labels mean what the repository does today, 9 October 2026.

## Implemented

- WPF shell on .NET 10 with a persistent sidebar, home grid, search, and settings
- Light, dark, system, and high-contrast appearance
- Local SQLite database with schema versioning and transactions
- AES-256-GCM for note and task text
- Windows DPAPI wrap of the data key, optional passphrase, recovery key, and lock
- Notes: create, edit, search in the list, delete, debounced save, flush on leave
- Todo: create, edit title and details, due date, priority, completion, filters, delete
- Backup export of the database and wrapped key
- Delete of the local vault
- Unit tests for crypto, vault behavior, migrations, and repositories
- No account, no telemetry, no network client

## Planned next

These fit the current module contract and do not need a server.

| Module | Why it is next | Constraint |
| --- | --- | --- |
| Journal | Same storage pattern as Notes, ordered by date | Encrypt the entry body. Show a saved state |
| Calendar | Local events only | Store instants with an offset. Do not claim Google or Outlook sync |
| Bookmarks and reading list | Small records, local | Encrypt URL and title. A URL is sensitive |

Calendar and Journal should land before any module that needs a network or a secrets policy.

## Deferred until a specific design exists

**Files and media.** Needs previews, size limits, and encryption of content and paths without loading whole videos into memory. Do not duplicate large files into the database. Not started.

**Wallet.** Payment-card numbers, bank passwords, and similar secrets do not belong in the notes table. A wallet needs its own item types, a short unlock, and a decision about what is never stored. Not started. Northpad is not a wallet in this version.

**Password and secrets manager.** Same constraint as Wallet. Not started.

**Messages.** A local draft list would be easy and misleading. Secure messaging needs identities, key distribution, a transport, delivery, and a review. None of that exists. The app does not show a Messages icon.

**Maps.** A useful map needs tiles or a native map control, an API key or a vendor account, network access, and attribution. Offline maps need a local dataset this app does not ship. A WebView would add a browser surface to a process that currently does not have one. Maps is not shown and is not described as offline.

**Contacts, clipboard manager, calculator, document viewer, knowledge base.** Possible later modules. Calculator is the only one with no storage or privacy question, and it is still out of scope until the journal and calendar path is proven.

**Accounts and synchronization.** Deferred until the local vault has been used and the sync design is written down. Requirements already fixed:

- The app remains usable with no account.
- Sign-in does not upload existing notes.
- Sync is opt-in and lists what leaves the device.
- The server stores ciphertext it cannot decrypt, or the feature is not called end-to-end encrypted.
- Refresh tokens, if any, are stored with DPAPI or the vault, not in a plaintext settings value.
- Conflict handling is specified before code is written.
- Logout does not delete the local vault unless the user asks.
- Account recovery must say that a recovery method the server can use is also a method the server can abuse.

No provider is selected. Do not add a cloud SDK in advance of that decision.

**Extension plugins.** Not planned. Third-party code in this process would need a separate permission and isolation design. New first-party modules are projects in the solution.

## Explicit non-goals for 0.1

- Cross-platform UI
- Windows Store packaging
- Auto-update
- Telemetry
- Encrypted timestamps and due dates
- Argon2id (the key file can carry a different KDF id later)
- Windows Hello
