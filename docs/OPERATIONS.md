# Operations

## Normal Day-To-Day Checks

Run all shell work only through the existing hidden/background launcher. Do not
open foreground PowerShell windows. Native children must also remain hidden and
bounded; use `UseShellExecute=false`/`CreateNoWindow=true` or the existing Node
launcher's `windowsHide=true`/`shell=false` settings.

Use the **registered executable**, not a hard-coded target-framework output
directory, to locate runtime state and logs. This read-only snippet is intended
for that hidden PowerShell launcher only:

```powershell
# Prerequisite: execute through the existing hidden/background launcher only.
$service = Get-CimInstance Win32_Service -Filter "Name='KrakenSupervisor'"
if (!$service -or $service.PathName -notmatch '^"([^"]+)"(?:\s|$)') {
    throw "Missing service or unexpected unquoted service executable path."
}
$serviceExe = $Matches[1]
if ([IO.Path]::GetFileName($serviceExe) -ne 'KrakenHost.exe' -or
    !(Test-Path -LiteralPath $serviceExe -PathType Leaf)) {
    throw "Unexpected or missing Kraken executable; inspect ownership first."
}
$releaseRoot = [IO.Path]::GetDirectoryName($serviceExe)
$service | Select-Object Name, State, ProcessId, PathName
Get-Content -LiteralPath (Join-Path $releaseRoot 'runtime\health.json')
Get-Content -LiteralPath (Join-Path $releaseRoot 'logs\controller.log') -Tail 50
Get-Content -LiteralPath (Join-Path $releaseRoot 'logs\supervisor.log') -Tail 50
```

For a separately staged release that is not installed, use its known staging
directory instead, and run only `--self-test` while live cooling owns hardware.
Do not treat files under that staged directory as evidence about the live service.

Confirm one service supervisor and one owned controller, fresh producer-matched
health, actual fan/pump RPM and duty, and whether any fault is LCD-only. A service
being `Running` or an executable having a recent timestamp is not sufficient.

## Emergency Actions

`--max-cooling` and `--reset-lcd` access hardware and require exclusive ownership.
Never run either command beside the existing live controller. Do not stop healthy
cooling just to let an emergency command acquire its mutex.

The stop, uninstall and CAM-restoration scripts are not diagnostic shortcuts.
Use lifecycle actions only with explicit operator authorization, independently
verified cooling fallback, a supervised single-owner handoff and rollback. Launch
authorized helpers hidden/background, and never start competing USB writers.

## Safe Operating Assumptions

- one controller process only
- CAM must not fight with KrakenHost for the same device
- do not add a second hardware watchdog
- do not add another service that also writes LCD or duty values

## Updating The Service

Do not stop cooling to build. With the .NET 10 SDK, publish `net10.0-windows` for
`win-x64` as a self-contained folder with trimming and single-file packaging off.
Use a new unique release directory and isolated output/intermediate directories;
see `INSTALL.md`. Run only its hidden, bounded `--self-test` first.
The installer refuses an existing service or any running KrakenHost process.
Replacing the live controller requires a separately verified cooling fallback,
observed pump/fan readback, a controlled single-owner handoff and a rollback.
Source changes and hardware-free tests do not establish that a handoff has passed;
use the actual qualification evidence and `SAFETY-VALIDATION.md` gates.

The supervisor restarts an exited controller when ownership is free. A live
unhealthy controller is preserved and reported in `supervisor.log`, rather than
being killed on uncertain health evidence. Investigate that report promptly.

`--max-cooling` is exclusive: it cannot run alongside the active controller.
Do not kill the active controller simply to make this command acquire its mutex.
The command verifies reported duties and RPM; invocation alone is not proof.

Routine telemetry logs every 30 seconds and on control-state changes. Active logs
rotate at 8 MiB, but archives are retained, not deleted. Total archive storage is
not capped; review retention deliberately instead of deleting diagnostics blindly.
