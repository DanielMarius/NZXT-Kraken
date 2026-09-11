# NZXT Kraken — v0.2

`KrakenHost` is a C#-only controller for NZXT Kraken LCD coolers on Windows.

It owns three responsibilities:

- read CPU, GPU, and liquid temperature
- control fan and pump duty
- render and upload the LCD frame

It also ships with a Windows service supervisor that checks controller health every 10 seconds and restarts an exited controller when hardware ownership is free.

## v0.2 Release And Qualification

Git tag `v0.2` identifies application version `0.2.0`, targeting .NET 10 LTS.
See [release notes](docs/RELEASE-v0.2.md) and the
[Drakula-PC qualification record](docs/QUALIFICATION-2026-09-11.md): 77
hardware-free checks and a measured 20-second firmware-hold commissioning test.
Those results do not authorize another machine's live upgrade or prove arbitrary
power-loss/USB-failure recovery. Windows 10 Pro 22H2 passed local testing but is
not in Microsoft's current .NET 10 supported-OS matrix.

Do not build over a running installation or run the installer to upgrade it.
Use separate output/intermediate directories and run the staged executable with
`--self-test` first. This entry point does not access hardware. See
`docs/SAFETY-VALIDATION.md` for the checks, limitations and live-handoff gate.
The installer now refuses existing services/processes rather than stopping them.

## What This Repo Contains

- `Program.cs`
  Main controller entry point and control loop.
- `KrakenNativeDevice.cs`
  Low-level Kraken HID and WinUSB communication.
- `KrakenSupervisor.cs`
  Supervisor logic and Windows service wrapper.
- `config.json`
  Runtime policy and display settings.
- `Install-KrakenSupervisorService.ps1`
  Builds and installs the Windows service from the local repo folder.
- `Uninstall-KrakenSupervisorService.ps1`
  Removes the Windows service.
- `Stop-KrakenCustomProcesses.ps1`
  Emergency stop helper for custom Kraken processes.
- `Restore-CAM.ps1`
  Optional fallback if you want to return control to NZXT CAM.

## How It Works

There are only two processes in the intended design:

- `KrakenHost.exe`
  The real controller. This is the only process that should touch the Kraken device during normal operation.
- `KrakenHost.exe --windows-service`
  The service supervisor. It watches producer-identified cooling health and starts a controller if one has exited and the controller mutex is free.

Health is based on real state, not only on process existence:

- controller process exists
- `runtime\health.json` is fresh
- status is `ok`
- pump RPM is at least `1500`
- fan RPM is at least `1000`
- snapshot PID/start identity matches the owned controller
- LCD-only failure is reported as degraded, not a reason to kill cooling

On failure, the supervisor:

- retries transient health reads without extending the last valid snapshot's age
- preserves a live unhealthy controller and logs that recovery needs operator attention
- starts a fresh controller only after the previous controller has exited and ownership is free
- never launches a competing emergency USB writer or kills processes by name

## Default Policy

- the hottest valid CPU/GPU/liquid temperature selects the duty
- at or above `40C`: fan `100%`, pump `100%`, held for at least `10s`
- return to `80%` only at or below `37C` after that hold (the configured `3C` hysteresis)
- missing/invalid temperature data: fan `100%`, pump `100%`
- configured controller loop interval: `1s` (fallback/default without configuration: `2s`)
- LCD minimum push interval: `2s`
- LCD maximum refresh interval: `30s`

## Requirements

- Windows
- .NET 10 SDK for building; the published `win-x64` release includes its runtime
- the validated NZXT Kraken Elite USB device `1e71:300c`; other models are not qualified
- NVIDIA GPU if you want GPU temperature through NVML
- NZXT CAM stopped or removed from startup so it does not fight for the same hardware

## Quick Start

All shell and console work must use the existing hidden/background launcher:
no foreground PowerShell windows, and hidden native child processes with bounded
timeouts. Do not open a console or stop cooling to follow these instructions.

1. Read `docs/INSTALL.md` and `docs/SAFETY-VALIDATION.md` first.
2. Publish `net10.0-windows` for `win-x64`, self-contained, with trimming and
   single-file packaging disabled. Use a new, unique release directory and
   separate build/intermediate directories; never overwrite a running release.
3. Run only the staged executable's hardware-free `--self-test` through the
   hidden launcher. A successful test does not authorize a live replacement.
4. Real-device testing requires independently verified fallback cooling and a
   supervised, exclusive handoff. Never run a second controller beside the owner.
5. During that separately authorized validation, confirm fan/pump readback, LCD,
   fresh producer-identified health, and clean controller logs. Derive the actual
   release directory as documented in `docs/OPERATIONS.md`; do not assume a TFM
   output folder is the installed runtime.

## Install As A Service

The installer is **fresh-install-only**, not an upgrade/restart command. After
independently verifying fallback cooling, invoke
`Install-KrakenSupervisorService.ps1 -CoolingFallbackVerified` only through the
existing hidden/background PowerShell launcher. An optional `-DotnetPath` selects
the verified .NET 10 SDK executable when it is not the default SDK.

The installer publishes a self-contained, untrimmed, non-single-file release to
`publish\net10.0-windows-win-x64-<UTC>-<GUID>\app`, with isolated build artifacts.
It requires `--self-test` to pass, then rechecks that no Kraken service or process
has appeared before service creation. Existing services/processes are preserved.

Read-only verification uses the actual quoted `KrakenSupervisor.PathName` to find
the release directory; see `docs/OPERATIONS.md`. Source/build documentation is not
evidence that a live handoff has passed.

Expected live processes:

- one `KrakenHost.exe --windows-service`
- one `KrakenHost.exe --interval=2 --min-lcd-push=2 --max-lcd-refresh=30`

## Runtime Output

Runtime output is relative to the **actual installed service executable's
directory**, not the repository's build folder:

- `<releaseRoot>\runtime\state.json`
- `<releaseRoot>\runtime\health.json`
- `<releaseRoot>\runtime\kraken_lcd.png`
- `<releaseRoot>\logs\controller.log`
- `<releaseRoot>\logs\supervisor.log`

The read-only path-resolution recipe in `docs/OPERATIONS.md` extracts
`<releaseRoot>` from the quoted `KrakenSupervisor.PathName` and fails closed if
the service path is unexpected.

## Emergency Commands

`--max-cooling` and `--reset-lcd` access hardware and require exclusive ownership.
Do not run either beside a live controller, or stop working cooling just to make
one acquire the mutex. An LCD-only fault is not permission to interrupt cooling.

The stop, uninstall and CAM-restoration helpers are lifecycle tools, not routine
diagnostics. Use them only under explicit operator authorization, after verifying
fallback cooling and arranging a supervised single-owner handoff. Any authorized
helper must run hidden/background; never launch competing CAM/USB writers.

## Documentation

Additional documentation is under `docs/`:

- `docs/INSTALL.md`
- `docs/ARCHITECTURE.md`
- `docs/OPERATIONS.md`
- `docs/TROUBLESHOOTING.md`
- `docs/RELEASE-v0.2.md`
- `docs/SAFETY-VALIDATION.md`
- `docs/QUALIFICATION-2026-09-11.md`
- `CHANGELOG.md`
- `CONTRIBUTING.md`

These files are written so they can also be copied into a Git wiki if needed.

## Uninstall

Uninstallation interrupts the cooling owner and is never an automatic cleanup
step. Review `Uninstall-KrakenSupervisorService.ps1` only after explicit operator
authorization and a verified cooling fallback/handoff. Use the existing hidden
launcher for an authorized operation; preserve recovery evidence and rollback.

## Contributing Rules

- Keep hardware ownership simple: one controller process only.
- Do not add Python runtime dependencies.
- Do not add watchdog loops that hammer the LCD.
- Do not let the supervisor become a second hardware controller.
- Stage configuration changes with a new release; never reinstall over a live
  service. Configuration changes require their own validation and authorized
  handoff.
- Follow `CONTRIBUTING.md` for repo workflow and review expectations.
