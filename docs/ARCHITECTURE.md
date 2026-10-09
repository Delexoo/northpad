# Implemented architecture

Status: implemented for version 0.1.0. This document describes the code in the repository, not future modules.

## Shape

Northpad is a modular monolith: one Windows process, one WPF window, and compile-time modules. There is no plugin loader, no second process, and no network client.

```
src/Northpad.App            shell, theme, settings, search, composition
src/Northpad.Modules.Notes  Notes view and view model
src/Northpad.Modules.Todo   Todo view and view model
src/Northpad.Core           vault, encryption, SQLite, repositories
tests/Northpad.Core.Tests   unit and storage tests
```

`Northpad.Core` does not reference WPF. The shell references the modules. Modules reference Core only. A new module is a project that exposes a view and view model, plus one registration in `ModuleRegistry` and one descriptor in `KnownModules`. `ModuleRegistry.EnsureMatchesCatalog` fails startup if those lists diverge.

## Shell

The main window is a 68-pixel sidebar and a content region. The sidebar contains the mark, Home, Search, and Settings. Apps are opened from the home grid, not pinned into the sidebar.

`ShellViewModel` owns navigation. Leaving a module calls `IFlushable.Flush` so Notes and Todo write pending edits before the view model is disposed. A second Northpad process exits if the local mutex `Local\Northpad.SingleInstance` is already held.

## Presentation

MVVM uses CommunityToolkit.Mvvm 8.4.2 source generators. Dependency injection uses `Microsoft.Extensions.DependencyInjection`. View models call repositories. Repositories do not reference views.

Colors are brushes in `Themes/Colors.xaml`. `ThemeManager` recolors them for light, dark, the Windows app theme, and high contrast. The choice is stored in the `settings` table under the key `theme`. Controls use shared styles in `Themes/Controls.xaml`.

## Storage

One SQLite database, `northpad.db`, opened through `Microsoft.Data.Sqlite`. `SqliteDatabase` serializes operations with a semaphore, sets WAL and `synchronous=FULL`, and applies schema version 2 inside a transaction. A database newer than this build is left untouched.

`NoteRepository` and `TaskRepository` are the only writers for their tables. Note title and body, and task title and details, are encrypted with a field-specific associated-data string that includes the record id. Other columns are plaintext and are listed in [DATA_STORAGE.md](DATA_STORAGE.md).

## Vault

`VaultService` creates a random 256-bit data key. Content encryption never uses the passphrase as the data key. The passphrase, when set, only wraps that key, so changing the passphrase does not rewrite notes.

Protection modes:

| Mode | Key file | Unlock |
| --- | --- | --- |
| Windows account | DPAPI blob for the current user | Automatic at startup |
| Passphrase | PBKDF2 wrap plus recovery-key wrap | Lock screen |

Setting a passphrase replaces the DPAPI blob. It does not leave a second copy that DPAPI can unwrap. The recovery key is shown once. Rotating it invalidates the previous recovery key. Removing the passphrase writes a new DPAPI wrap of the same data key.

`ContentProtector` refuses to encrypt or decrypt while the session is locked. `VaultSession` keeps the data key in a pinned array and zeroes it on lock.

## Logging

`SafeFileLogger` writes to `%LOCALAPPDATA%\Northpad\logs\northpad.log`. It records the formatted message and the exception type. It does not write `Exception.ToString()`, so stack traces are not copied into the log. Call sites must not pass note text, task text, passphrases, or keys as log values.

## What the shell does not do

The shell does not open network sockets, host a WebView, load assemblies from the data folder, or sync to an account. Settings text that mentions accounts and synchronization states that those features are absent.
