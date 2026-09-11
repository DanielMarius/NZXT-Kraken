# Install Guide

## Prerequisites

For an already-running installation, stop here: use side-by-side validation in
`SAFETY-VALIDATION.md`, not the normal output folder. Existing cooling must remain
running until an independently verified fallback and supervised handoff exist.

- Windows machine with the supported NZXT Kraken LCD cooler attached
- .NET 10 SDK for publishing; the self-contained release includes its runtime
- An existing hidden/background PowerShell launcher, elevated only for an
  explicitly authorized fresh service installation
- NZXT CAM not actively controlling the same device

All shell and native console children must remain hidden/background, with bounded
timeouts. Do not open a foreground PowerShell console. Native launchers must use
`UseShellExecute=false` and `CreateNoWindow=true` (or `windowsHide=true` and
`shell=false` in an existing Node launcher). Do not add a polling/helper service.

## Stage And Validate First

Publish `KrakenHost.csproj` with the verified .NET 10 SDK using Release,
`net10.0-windows`, `win-x64`, and `--self-contained true`. Set
`PublishSingleFile=false`, `PublishTrimmed=false`, `UseSharedCompilation=false`
and `nodeReuse:false`. Choose a new timestamp/GUID release directory; put the
publish output, `BaseOutputPath`, and `BaseIntermediateOutputPath` in separate
subdirectories of that new location. Never use a running installation's output
or intermediate directory.

Run the published `KrakenHost.exe --self-test` only through the existing hidden
launcher. This hardware-free entry point returns before controller construction,
mutex acquisition, telemetry or USB access. Retain its result and staged files.
Passing self-tests does not prove a live cooling handoff or authorize one.

Do not start the staged executable without `--self-test` beside an existing
controller. Real-device validation requires explicit authorization, independently
verified fallback cooling, exclusive ownership, pump/fan readback and rollback.

During that separately authorized hardware validation, confirm:

- LCD renders correctly
- fan and pump respond with observed RPM and duty
- the actual release's `runtime\health.json` updates with the owned producer
- controller log stays clean

## Install The Supervisor Service

For **fresh installation only**, have the existing hidden/background PowerShell
launcher invoke `Install-KrakenSupervisorService.ps1` with
`-CoolingFallbackVerified`. If necessary, supply `-DotnetPath` with the full path
to the verified .NET 10 SDK's `dotnet.exe`; the default resolves `dotnet` on PATH.
The fallback switch attests to an independent hardware safety check, not a way
to bypass it. Do not change machine execution policy to work around a failure.

The installer:

1. Refuses any existing `KrakenSupervisor` service or `KrakenHost` process.
2. Publishes a unique self-contained folder at
   `publish\net10.0-windows-win-x64-<UTC>-<GUID>\app`, with separate `bin` and `obj`
   directories, no trimming, and no single-file packaging.
3. Requires its hardware-free `--self-test` to pass and rechecks service/process
   ownership before creating the service.
4. Registers the **published executable's quoted path**, then starts the new
   service. It is not an upgrade tool and never stops an existing owner.

## Verify Installation

Use the read-only recipe in `OPERATIONS.md` through the hidden launcher. Resolve
the release root from the quoted `KrakenSupervisor.PathName`; do not substitute
the latest build folder. Inspect health/log files beneath that actual directory.

Expected:

- service status is `Running`
- one supervisor process
- one controller process
- fresh health matching the controller PID/start identity and valid cooling
- observed pump/fan readback and reviewed LCD status

These checks apply to each installation; they are not automatic proof for a new
machine. Drakula-PC's recorded handoff is in `QUALIFICATION-2026-09-11.md`.
Retain the relevant machine's actual evidence before declaring completion.

## Uninstall

Do not uninstall as part of an upgrade or troubleshooting shortcut. Any explicit
uninstall request must first establish independent fallback cooling, a supervised
single-owner handoff and rollback. Review the uninstall helper before executing
it, and use only the existing hidden/background launcher.
