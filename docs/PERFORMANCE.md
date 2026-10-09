# Performance measurements

These numbers were measured. They are not targets and they are not promises for other machines.

## Machine

Measured 9 October 2026.

| Item | Value |
| --- | --- |
| OS | Windows build 10.0.26200. `Get-ComputerInfo` reported the product name Windows 10 Home |
| CPU | 12th Gen Intel Core i7-12700H, 14 cores, 20 logical processors |
| RAM | 16,865,492,992 bytes |
| SDK | .NET SDK 10.0.103 |
| Build | Release, `win-x64`, framework-dependent (`--self-contained false`) |

Framework-dependent means the published folder does not contain the .NET runtime. The machine already had the .NET 10 Windows Desktop runtime.

## Publish size

`artifacts/publish-fd` after `dotnet publish -c Release -r win-x64 --self-contained false`:

**27,459,585 bytes (26.2 MiB)**

That is the application folder only. A self-contained publish was not measured and would be larger because it includes the runtime.

## Startup

The app logs `Application ready in N ms` after the vault is opened and the first view is created, immediately after the window is shown. Four launches of the fixed release build, without a reboot between them:

| Launch | Ready |
| --- | --- |
| 1 | 627 ms |
| 2 | 600 ms |
| 3 | 596 ms |
| 4 | 598 ms |

A cold start after a reboot was not measured. An earlier launch that threw during theme setup (797 ms, then an error screen) is not included. That theme bug was fixed before these four runs.

## Memory and idle CPU

Sampled with `Get-Process` on the release process:

| Moment | Working set | Private bytes |
| --- | --- | --- |
| About 2 seconds after ready, home screen | 230,776,832 bytes | 181,567,488 bytes |
| After opening Notes | 261,779,456 bytes | not re-sampled |

During a 2 second idle interval on the home screen, `TotalProcessorTime` did not increase (0 ms). That is one sample, not a long trace.

No background timer, poll loop, or network client is installed. Editor saves are debounced on the UI thread and write one SQLite transaction.

## Storage microbench

From `PerformanceBaselineTests` on the same machine, Debug test host, DPAPI vault, 9 October 2026:

| Operation | Result |
| --- | --- |
| 200 AES-256-GCM encryptions of 2 KB | 1.0 ms |
| 50 encrypted note inserts of about 1 KB | 181.9 ms |
| List and decrypt those 50 notes | 1.1 ms |

The test asserts only that each step finished in under 30 seconds, so a slow machine does not fail the suite on a tight budget. The console line is the measurement.

## Not measured

- Startup immediately after boot
- Self-contained publish size
- Memory after thousands of notes
- UI frame time and animation cost
- A comparison build of WinUI, Avalonia, Tauri, or Electron on this machine

Those are listed in the research document as open measurements. They were not invented to fill the table.
