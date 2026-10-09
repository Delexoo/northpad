# Development

## Layout

- `Northpad.slnx` is the solution.
- `global.json` asks for the .NET 10.0.1xx SDK and rolls forward to the latest feature band.
- `Directory.Build.props` enables nullable reference types.
- Application code uses file-scoped namespaces and treats warnings from the app build as defects. The Release and Debug app builds used during development completed with no warnings.

## Conventions

- Core types do not reference WPF.
- Modules do not reference each other or the shell project.
- Repositories are the storage boundary. Views do not issue SQL.
- Do not log note text, task text, passphrases, recovery keys, or key bytes.
- Do not add a package when the base class library already provides the behavior. Document the choice when a package is added.
- New modules need a `KnownModules` entry and a `ModuleRegistry` arm. Startup checks that those lists match.
- Schema changes need a new migration that runs inside the existing transaction helper, plus a test that an older file migrates and a newer version is refused.

## Commands

```powershell
dotnet build Northpad.slnx
dotnet test Northpad.slnx
dotnet run --project src/Northpad.App
```

Tests use a temporary directory under `%TEMP%\northpad-tests` and DPAPI for the current user. Passphrase tests use 1,000 PBKDF2 iterations so the suite stays fast. Production vaults use 600,000. One test asserts that the production constant is at least 600,000.

## Packages

Direct packages, all version-checked on 9 October 2026:

| Package | Version | Where |
| --- | --- | --- |
| Microsoft.Data.Sqlite | 10.0.12 | Core |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | Core |
| System.Security.Cryptography.ProtectedData | 10.0.12 | Core |
| CommunityToolkit.Mvvm | 8.4.2 | App, Notes, Todo |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | App |
| Microsoft.Extensions.Logging | 10.0.12 | App |
| xunit | 2.9.3 | Tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | Tests |

`dotnet list package --vulnerable --include-transitive` reported no vulnerable packages for the app, core, and test projects against nuget.org on 9 October 2026. The audit feed timestamp used during restore was 8 October 2026. Re-run that command before a release. It is not a guarantee that a vulnerability published later is absent.

## UI checks

The automated tests cover cryptography, the vault, SQLite migrations, and repositories. They do not drive the WPF window. After UI changes, run the app and exercise Home, Notes, Todo, Search, and Settings, including a window resize. Accessibility names are set on icon-only sidebar buttons and on the main editor fields.
