# Roadmap

Labels mean what the repository does today, 9 October 2026.

## Implemented

- WPF shell on .NET 10 with a persistent sidebar, home grid, local search, and settings
- Light, dark, system, and high-contrast appearance
- Local SQLite database, schema version 2, transactions
- AES-256-GCM for note and task text, document payloads, and imported files
- Windows DPAPI wrap of the data key, optional passphrase, recovery key, and lock
- Notes with blocks (text, heading, list, to-do, quote, code, divider, picture)
- Todo, Calendar, Reminders, Mail, Photos, Drive, Sheets, Passwords, a glossary, and a private ledger
- Search (SearXNG), browser, OpenStreetMap, and YouTube through the Piped frontend, all idle until network tools are allowed
- No account, no telemetry, no AI features

## Limits that are intentional

- Mail is stored here and is not sent to an email provider.
- Reminders fire only while Northpad is open.
- Translate uses the glossary unless you set your own LibreTranslate address.
- Wallet does not store card numbers or talk to a bank.
- Maps do not read your location. Tiles come from OpenStreetMap after you allow network.
- YouTube does not use a Google account. Playback can still request video data from YouTube's servers.
- The browser uses Windows WebView2. A page you open can see that visit.
- Imported files are limited to 25 MB.

## Planned next

Journal, bookmarks, and a reading list fit the same local document store. They are not in the app yet.

## Deferred

**Messages to other people.** Mail in this version is a local store. Sending and receiving through a provider is not implemented.

**Card wallet and bank connections.** The ledger does not store payment cards.

**Offline maps.** There is no local map dataset. Panning requests OpenStreetMap tiles.

**Contacts, clipboard manager, calculator.** Not in this version.

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
