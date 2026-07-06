# Kraken Windows 10

`KrakenHost` is a C#-only controller for NZXT Kraken LCD coolers on Windows.

It owns three responsibilities:

- read CPU, GPU, and liquid temperature
- control fan and pump duty
- render and upload the LCD frame

It also ships with a Windows service supervisor that checks controller health every 10 seconds and restarts it if needed.

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
  The service supervisor. It watches controller health and starts a controller if one is missing or unhealthy.

Health is based on real state, not only on process existence:

- controller process exists
- `runtime\health.json` is fresh
- status is `ok`
- pump RPM is at least `1500`
- fan RPM is at least `1000`
- LCD status is not reporting repeated upload failure

On failure, the supervisor:

- invokes emergency `--max-cooling`
- kills the broken controller process
- starts a fresh controller instance

## Default Policy

- below `40C`: fan `80%`, pump `80%`
- at or above `40C`: fan `100%`, pump `100%`
- controller loop interval: `2s`
- LCD minimum push interval: `2s`
- LCD maximum refresh interval: `30s`

## Requirements

- Windows
- .NET 7 SDK
- NZXT Kraken device connected to the machine
- NVIDIA GPU if you want GPU temperature through NVML
- NZXT CAM stopped or removed from startup so it does not fight for the same hardware

## Quick Start

1. Clone the repo.
2. Open PowerShell in the repo folder.
3. Build the project:

```powershell
dotnet build .\KrakenHost.csproj -c Release
```

4. Run a manual foreground test:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --interval=2 --min-lcd-push=2 --max-lcd-refresh=30
```

5. Confirm:

- fan and pump respond
- LCD looks correct
- `bin\Release\net7.0-windows\runtime\health.json` updates
- `bin\Release\net7.0-windows\logs\controller.log` shows no repeated LCD upload failures

## Install As A Service

After manual testing is stable:

```powershell
.\Install-KrakenSupervisorService.ps1
```

If PowerShell blocks local script execution:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-KrakenSupervisorService.ps1
```

Verify the live state:

```powershell
Get-Service KrakenSupervisor
Get-Process KrakenHost
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
```

Expected live processes:

- one `KrakenHost.exe --windows-service`
- one `KrakenHost.exe --interval=2 --min-lcd-push=2 --max-lcd-refresh=30`

## Runtime Output

Runtime output is written next to the built executable:

- `bin\Release\net7.0-windows\runtime\state.json`
- `bin\Release\net7.0-windows\runtime\health.json`
- `bin\Release\net7.0-windows\runtime\kraken_lcd.png`
- `bin\Release\net7.0-windows\logs\controller.log`
- `bin\Release\net7.0-windows\logs\supervisor.log`

## Emergency Commands

Force maximum cooling:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --max-cooling
```

Reset the LCD:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --reset-lcd
```

Stop custom Kraken processes:

```powershell
.\Stop-KrakenCustomProcesses.ps1
```

Restore CAM if needed:

```powershell
.\Restore-CAM.ps1
```

## Documentation

Additional documentation is under `docs/`:

- `docs/INSTALL.md`
- `docs/ARCHITECTURE.md`
- `docs/OPERATIONS.md`
- `docs/TROUBLESHOOTING.md`

These files are written so they can also be copied into a Git wiki if needed.

## Uninstall

```powershell
.\Uninstall-KrakenSupervisorService.ps1
```

If needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-KrakenSupervisorService.ps1
```

## Contributing Rules

- Keep hardware ownership simple: one controller process only.
- Do not add Python runtime dependencies.
- Do not add watchdog loops that hammer the LCD.
- Do not let the supervisor become a second hardware controller.
- If you change `config.json`, rebuild before reinstalling the service.
