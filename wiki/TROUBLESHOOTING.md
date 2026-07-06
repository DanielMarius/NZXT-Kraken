# Troubleshooting

## LCD Problems

Check:

- CAM is not running
- only one controller process exists
- `controller.log` has no repeated LCD upload failures

Recovery:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --reset-lcd
.\Stop-KrakenCustomProcesses.ps1
```

## Fan Or Pump Problems

Check:

```powershell
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
```

Use this immediately if needed:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --max-cooling
```

## Service Problems

If install or restart fails:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-KrakenSupervisorService.ps1
```
