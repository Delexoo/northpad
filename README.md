# northpad

Northpad is a local-first Windows desktop workspace. It opens Notes and Todo inside one window, keeps their text encrypted on disk, and does not require an account or a network connection.

Version 0.1.0 includes the application shell, settings, Notes, and Todo. Calendar, Journal, Files, Wallet, Maps, Messages, and synchronization are not in this version. See [docs/ROADMAP.md](docs/ROADMAP.md).

## Privacy in this version

- Notes and task text are encrypted with AES-256-GCM before they are written.
- The data key is wrapped with Windows DPAPI for the current user, or with a passphrase and a recovery key if you set one.
- Northpad does not contain telemetry and does not upload notes, tasks, or keys.
- Timestamps, task completion, priority, and due dates are stored unencrypted. Details are in [docs/DATA_STORAGE.md](docs/DATA_STORAGE.md).
- Northpad cannot hide data from the operating system, from other programs running as you while the workspace is unlocked, or from someone who can already read process memory.

## Requirements

- Windows 10 version 1809 or later (the project targets Windows 10.0.17763).
- .NET 10 SDK to build. The .NET 10 Windows Desktop runtime is required to run a framework-dependent build.

This repository was developed with the .NET SDK 10.0.103.

## Build and run

```powershell
dotnet build Northpad.slnx
dotnet test Northpad.slnx
dotnet run --project src/Northpad.App
```

A framework-dependent publish for 64-bit Windows:

```powershell
dotnet publish src/Northpad.App/Northpad.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish-fd
```

The published folder still needs the .NET 10 desktop runtime on the computer that runs it.

## Where data lives

`%LOCALAPPDATA%\Northpad`

That folder contains `northpad.db`, `vault.key`, and `logs`. Northpad creates it on first launch. Nothing is sent off the computer.

## First-run protection

The first launch creates a vault protected by your Windows account. You can open Northpad without a passphrase. In Settings you can set a passphrase. After that, Northpad starts locked, and the Windows account alone cannot unwrap the key. Save the recovery key when it is shown. Northpad cannot restore a forgotten passphrase without that key.

## Documentation

- [Architecture research](docs/ARCHITECTURE_RESEARCH.md)
- [Implemented architecture](docs/ARCHITECTURE.md)
- [Threat model](docs/THREAT_MODEL.md)
- [Security](docs/SECURITY.md)
- [Data storage](docs/DATA_STORAGE.md)
- [Development](docs/DEVELOPMENT.md)
- [Roadmap](docs/ROADMAP.md)
- [Performance measurements](docs/PERFORMANCE.md)
