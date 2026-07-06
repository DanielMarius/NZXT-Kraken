# Operations

## Normal Day-To-Day Checks

Useful checks:

```powershell
Get-Service KrakenSupervisor
Get-Process KrakenHost
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
Get-Content .\bin\Release\net7.0-windows\logs\controller.log -Tail 50
```

## Emergency Actions

Force full cooling:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --max-cooling
```

Reset LCD:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --reset-lcd
```

Stop custom processes:

```powershell
.\Stop-KrakenCustomProcesses.ps1
```

Restore CAM:

```powershell
.\Restore-CAM.ps1
```

## Safe Operating Assumptions

- one controller process only
- CAM must not fight with KrakenHost for the same device
- do not add a second hardware watchdog
- do not add another service that also writes LCD or duty values

## Updating The Service

1. Stop the service.
2. Rebuild the project.
3. Re-run `Install-KrakenSupervisorService.ps1`.

The install script already handles stop, rebuild, delete, create, and restart.
