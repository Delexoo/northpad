# Architecture research

Research date: 9 October 2026. The machine used for environment checks and later measurements is described in [PERFORMANCE.md](PERFORMANCE.md). Web pages below were fetched or searched on that date. Where a page could not be retrieved, the note says so.

## 1. Product and technical requirements

Northpad is a Windows desktop shell for a small set of local tools. The first release has to:

- Run without an account or a network
- Keep one window, with a stable sidebar and a home grid
- Stay responsive on a normal laptop
- Encrypt sensitive text at rest with a maintained algorithm
- Add a module without rewriting the shell
- Avoid telemetry, background services, and fake security controls
- Leave room for accounts and sync without requiring them

It does not have to be cross-platform. It does not have to host third-party plugins.

## 2. Research methodology

The environment was inspected first: Windows build 10.0.26200, .NET SDK 10.0.103, Windows Desktop runtimes 8.0.23 and 10.0.3, Node 24.15.0, Rust 1.95.0. No Windows App SDK workload was installed.

Framework, storage, and cryptography choices were checked against Microsoft Learn, SQLite's site, OWASP, NuGet, and the project sites for Tauri, Electron, and Avalonia. Blog benchmarks were not used as scores. If a number was not measured here, it is marked unknown.

After the WPF application existed, publish size, startup, memory, idle CPU, and a SQLite and AES microbench were measured on this machine. See [PERFORMANCE.md](PERFORMANCE.md).

## 3. Framework comparison

Weights reflect the product, not a generic "best UI toolkit" ranking.

| Criterion | Weight | Why this weight |
| --- | --- | --- |
| Security and privacy | 20 | Local text must not depend on a bundled browser or a background service |
| Low resource use | 15 | The brief asks for a light desktop app |
| Windows integration | 15 | DPAPI, UI Automation, and native windows are part of the product |
| Maintainability | 13 | .NET 10 is the installed LTS line |
| Responsiveness | 12 | The shell is a daily driver |
| Development complexity | 10 | One language and the installed SDK reduce risk |
| Modularity | 8 | A modular monolith is enough |
| Packaging | 7 | A folder publish is acceptable for 0.1 |

Scores are 1 to 5. They are judgments from the documentation review, not measurements of finished Northpad clones in every stack. A higher score is better for Northpad.

| Criterion | WPF on .NET 10 | WinUI 3 | Avalonia 11 | Tauri 2 | Electron |
| --- | ---: | ---: | ---: | ---: | ---: |
| Security and privacy | 5 | 5 | 4 | 3 | 2 |
| Low resource use | 4 | 3 | 3 | 4 | 2 |
| Windows integration | 4 | 5 | 3 | 3 | 3 |
| Maintainability | 5 | 4 | 4 | 3 | 3 |
| Responsiveness | 4 | 4 | 4 | 3 | 3 |
| Development complexity | 5 | 3 | 4 | 2 | 3 |
| Modularity | 4 | 4 | 4 | 4 | 4 |
| Packaging | 4 | 3 | 4 | 4 | 3 |
| Weighted total | 443 | 403 | 370 | 320 | 273 |

The maximum is 500. The total is a summary of the judgment above. WPF was selected because the reasons below held, not because 443 is a precise measurement.

### WPF on .NET 10

.NET 10 is LTS, released November 2025, supported until 14 November 2028. WPF is documented as a supported .NET 10 desktop UI, including a "What's new in WPF for .NET 10" page. The Windows Desktop runtime was already installed. The app uses UI Automation peers that WPF controls expose, DPAPI, and the system theme registry key `AppsUseLightTheme`.

Installed size, idle memory, and startup of a Northpad-sized WPF app were unknown at decision time. They were measured after implementation and are in the performance document. Packaging is `dotnet publish` framework-dependent or self-contained. There is no separate UI runtime.

### WinUI 3 and the Windows App SDK

Microsoft's deployment docs say unpackaged apps must bootstrap the Windows App SDK and need the Visual C++ redistributable. Framework-dependent deployment needs the runtime installed. Self-contained deployment "significantly increases output size" and, for unpackaged apps, uses more memory because pages are not shared. Windows App SDK 1.8.12 was listed on 24 September 2026 in the downloads archive. This machine had no Windows App SDK workload.

WinUI is the stronger match for current Windows visuals. Northpad's brief is a custom monochrome UI, not Fluent. The extra runtime and bootstrap were not worth it for version 0.1. Idle memory of a WinUI Northpad was not measured. It is unknown.

### Avalonia 11

Avalonia 11.3.17 was published on 27 May 2026. It draws with Skia, including on Windows. That is a good portable UI and an unnecessary second renderer for a Windows-only app. A 2026 blog comparison quoted Avalonia 11.2 startup and memory figures. Those figures are not used here. They were not reproduced, and they are not official Avalonia numbers. Northpad's Avalonia memory and startup are unknown.

### Tauri 2

Tauri uses the system WebView2 on Windows and documents that as a security advantage over bundling a browser, because the OS ships WebView patches. Official installer notes put a fixed WebView2 runtime near an extra 180 MB and an offline installer near 127 MB. The default bootstrapper adds no payload when WebView2 is already present. Rust was installed, so the toolchain was available.

The whole UI would still be a web view: a second language, a DOM accessibility tree, and a browser engine inside an app that does not need remote documents. Maps might want a web view later. That can be an isolated decision. It should not define the shell.

### Electron

Electron's own documentation says it bundles Chromium and Node, and that most apps are larger than 100 MB on disk because of that bundle. The process model is a main process plus a renderer per window. That is the heaviest option for a local text workspace and the largest web attack surface. It was not measured on this machine. It is the comparison point, not a candidate.

### Other options

WinForms is supported on .NET 10 and is lighter to draw, but a custom home grid, theme, and typography fight the control set. .NET MAUI is aimed at cross-platform and mobile and brings another stack. Neither changed the WPF decision.

## 4. Storage comparison

| Option | Fit |
| --- | --- |
| SQLite through Microsoft.Data.Sqlite | Selected. One file, transactions, WAL, official provider |
| SQLCipher via `SQLitePCLRaw.bundle_e_sqlcipher` | Rejected for 0.1. Microsoft documents that build as unsupported |
| JSON or one file per note | Rejected as the primary store. Fine for settings, weak for transactions and crash recovery |
| Multiple databases | Rejected. One vault backup and one migration story is enough |

What belongs in SQLite: notes, tasks, theme, schema version. What belongs on the filesystem: the wrapped key, logs, and future attachment blobs. Attachments should not be stored as database BLOBs when Files is built. Large media stays as encrypted files referenced by id.

One database is enough. Modules do not open their own connections. `SqliteDatabase` is the shared gate. Schema changes are versioned. WAL crash recovery is documented by SQLite. `synchronous=FULL` favors durability over the faster `NORMAL` mode.

Encrypted text is not indexed in SQL. Search decrypts in memory. The plaintext columns are an explicit metadata leak, documented in the threat model. Database encryption and field encryption are different. A passphrase on the database would not, by itself, define field authentication, recovery keys, or which columns stay searchable. Field encryption is what this version implements. Full-file encryption can be revisited if metadata leakage becomes unacceptable.

## 5. Cryptography and key management

AES-GCM is provided by `System.Security.Cryptography.AesGcm` on Windows. Microsoft documents 12-byte nonces and says a nonce must not be reused with the same key. Northpad uses a 32-byte key, a fresh 12-byte nonce, and a 16-byte tag, and passes the tag size to the constructor so truncation is rejected.

DPAPI through `ProtectedData` is the Windows-supported way to bind a secret to the current user without storing a passphrase. Microsoft warns that `LocalMachine` scope allows any process on the computer to unwrap data. Northpad uses `CurrentUser` only.

Password hashing guidance from OWASP, retrieved 9 October 2026, prefers Argon2id (for example 19 MiB, t=2, p=1) and gives PBKDF2-HMAC-SHA256 a floor of 600,000 iterations when a FIPS-oriented or available alternative is required. .NET 10 marks the older `Rfc2898DeriveBytes` constructors obsolete and provides `Rfc2898DeriveBytes.Pbkdf2`, which is what the code calls. Argon2 was not adopted for the maintenance and license reasons in [SECURITY.md](SECURITY.md).

Windows Hello and hardware-backed keys were not implemented. They are a reasonable later unlock factor. They do not replace encryption of the database.

Key rotation of the data key is not implemented. Passphrase changes rewrap the same data key. Recovery-key rotation rewraps it under a new recovery secret. Losing both the passphrase and the recovery key is not recoverable. That is stated in the UI when the recovery key is shown.

Randomness is `RandomNumberGenerator`. There is no custom RNG and no custom construction beyond the documented payload layout.

Memory: the data key is pinned and zeroed on lock. Passphrase strings cannot be zeroed. Logs must not receive secrets. Crash dumps remain an OS issue.

Secure delete of ciphertext on an SSD is not solved in software by overwrite. The threat model says so.

## 6. UI architecture

MVVM matches WPF data binding. CommunityToolkit.Mvvm is maintained by the .NET team, version 8.4.2 on 25 March 2026, and it removes most `INotifyPropertyChanged` boilerplate without a second UI framework.

A heavier application framework was not added. Navigation is a shell view model. Dependency injection is the built-in service collection. There is no ReactiveUI, Prism, or separate state store.

Shared brushes and control styles are the design system. Modules do not restyle the window.

Background work is limited to PBKDF2 on a worker thread during unlock, and SQLite writes. Editor saves are debounced on the dispatcher so a keystroke and a flush cannot reorder.

Accessibility: sidebar icon buttons have names and tooltips. Editors have automation names. Keyboard focus is visible on the shared button and input templates. High contrast follows `SystemParameters.HighContrast` and system colors. A full high-contrast visual audit with a screen reader was not completed.

UI tests are not automated. Logic that can run without a window is covered in `Northpad.Core.Tests`. The desktop pass on 9 October 2026 opened the release build, confirmed the home grid, opened Notes, and opened Settings with the account and sync copy visible.

## 7. Accounts and synchronization

Accounts are deferred. The local vault does not reference a user id. Settings states that there is no account and that nothing is uploaded. A later account system can sit beside `VaultService` if it never receives the data key. The rules are in [ROADMAP.md](ROADMAP.md). No cloud SDK is referenced.

## 8. Dependency assessment

Direct dependencies are listed in [DEVELOPMENT.md](DEVELOPMENT.md). Licenses are the standard permissive .NET and SQLite stack (MIT / SQLite blessing for the bundled engine). The vulnerability audit command reported no known issues on 9 October 2026. SQLitePCLRaw 2.1.12 is the native SQLite bundle pulled in by Microsoft.Data.Sqlite 10.0.12. Measured SQLite version: 3.53.3.

Rejected dependencies: SQLCipher's unsupported bundle, Konscious and Isopoh Argon2, Serilog, Entity Framework, a web UI kit, and any sync provider.

## 9. Recommended architecture

WPF modular monolith on .NET 10, Microsoft.Data.Sqlite, application-level AES-256-GCM, DPAPI by default, optional PBKDF2 passphrase wrap, compile-time modules. That is what was built. [ARCHITECTURE.md](ARCHITECTURE.md) describes the result.

## 10. Alternatives rejected

- WinUI 3: better Windows visuals, extra runtime and bootstrap, workload not installed, custom theme does not need WinUI controls.
- Avalonia: portability that the product does not require, Skia beside an existing Windows UI stack.
- Tauri: small native core, but the UI would be WebView2 and a second language.
- Electron: bundled Chromium, documented disk cost, larger attack surface.
- SQLCipher: unsupported provider, and database encryption would still need the field and key design.
- Argon2 packages reviewed: maintenance or license problems relative to in-box PBKDF2.
- Plugins and microservices: no isolation story and no operational need.
- A messages or maps placeholder: would imply a capability that does not exist.

## 11. Known risks and unresolved questions

- Idle working set on this laptop was about 220 MiB. That may be higher than the "lightweight" goal suggests. It has not been compared with a WinUI build on the same machine.
- Metadata columns are plaintext.
- PBKDF2 is slower to upgrade than storing an algorithm id from day one would have been. The iteration count is stored. The algorithm is not a separate id yet. A future Argon2 wrap needs a versioned mode in `vault.key`.
- Windows Hello is unimplemented.
- Cold start after reboot was not measured.
- Screen-reader behavior was not tested with Narrator beyond automation names.
- Secure deletion and crash-dump exposure are unsolved.
- Whether Calendar should store a time zone id per event is open. It must not be a bare UTC instant that shifts the wall-clock day.

## 12. Security assumptions and threat model

See [THREAT_MODEL.md](THREAT_MODEL.md) and [SECURITY.md](SECURITY.md). The short form: Northpad protects note and task text at rest against another local user and against a copied DPAPI vault. It does not protect an unlocked process from the same user, and it does not protect anyone from a hostile operating system.

## 13. Performance measurement plan

Done for the WPF build, in [PERFORMANCE.md](PERFORMANCE.md):

1. Publish Release `win-x64` framework-dependent and sum the folder.
2. Launch, read the ready log line, sample working set and CPU.
3. Keep the microbench test that prints encrypt and insert times.
4. Repeat startup after a reboot before calling a number a cold start.
5. If a second UI stack is ever prototyped, publish it the same way on this machine before comparing.

## 14. Migration and future expansion

Schema version 1 is the only version. A later version must run in a transaction and refuse to open a newer file. Key-file format byte 1 is Windows DPAPI or passphrase. A new KDF adds a format or mode byte. It does not reuse the old layout silently.

Modules are added in-process. Sync, if it is built, is a service the repositories can call after the user opts in. It is not a table prefix added in secret.

## 15. References

Accessed 9 October 2026 unless noted.

- .NET 10 WPF: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- .NET 10 supported OS list: https://github.com/dotnet/core/blob/20e72eb1b769d71b4dd208419d66d8a0ef3b1961/release-notes/10.0/supported-os.md
- Desktop guide index: https://learn.microsoft.com/en-us/dotnet/desktop/
- Windows App SDK deployment overview: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
- Unpackaged Windows App SDK apps: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
- Unpackaged WinUI distribution: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app
- Windows App SDK 1.8 downloads archive: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads-archive
- Avalonia 11.3.17 release: https://github.com/AvaloniaUI/Avalonia/releases/tag/11.3.17
- Tauri security: https://v2.tauri.app/security/
- Tauri Windows installer and WebView2 sizes: https://v2.tauri.app/distribute/windows-installer/
- Electron why-Electron (bundled Chromium, apps often over 100 MB): https://www.electronjs.org/docs/latest/why-electron
- Electron process model: https://github.com/electron/electron/blob/main/docs/tutorial/process-model.md
- Cross-platform cryptography, AES-GCM nonce and tag sizes: https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography
- `AesGcm` constructors and tag size: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.-ctor?view=net-10.0
- `AesGcm.Decrypt`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.decrypt?view=net-10.0
- `Rfc2898DeriveBytes` and `Pbkdf2`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes?view=net-10.0
- DPAPI `ProtectedData`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata?view=net-10.0
- How to use data protection: https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection
- `CryptProtectData`: https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata
- Microsoft.Data.Sqlite encryption: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption
- Microsoft.Data.Sqlite 10.0.12: https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12
- SQLite WAL: https://www.sqlite.org/wal.html
- OWASP Password Storage Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
- MVVM Toolkit: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- CommunityToolkit.Mvvm 8.4.2: https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2
- Konscious Argon2 1.3.1 (reviewed, not used): https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2/1.3.1
- Isopoh Argon2 (reviewed, not used): https://www.nuget.org/packages/Isopoh.Cryptography.Argon2/

The WPF UI Automation overview URL `https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/ui-automation-overview` returned 404 on 9 October 2026. WPF controls in this app are standard controls with `AutomationProperties.Name` set on icon-only and unlabeled inputs. A replacement Microsoft page was not fetched after that 404.

Windows UI Automation is documented at https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32 (not re-fetched as HTML during this session; the 404 above is the page that was checked).
