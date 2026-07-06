# Operations

## Useful Checks

```powershell
Get-Service KrakenSupervisor
Get-Process KrakenHost
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
Get-Content .\bin\Release\net7.0-windows\logs\controller.log -Tail 50
```

## Emergency Commands

Maximum cooling:

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
