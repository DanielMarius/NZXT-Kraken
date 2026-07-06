# Troubleshooting

## LCD Is Corrupted Or Flickering

Check:

- no NZXT CAM process is running
- only one `KrakenHost.exe` controller exists
- `controller.log` is not showing repeated LCD upload failures

Recovery actions:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --reset-lcd
.\Stop-KrakenCustomProcesses.ps1
```

Then restart the service or run the controller manually.

## Fan Or Pump Looks Wrong

Check the health payload:

```powershell
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
```

Expected thresholds:

- pump RPM >= `1500`
- fan RPM >= `1000`

Force safe cooling immediately if needed:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --max-cooling
```

## Service Will Not Install

Common reasons:

- PowerShell execution policy blocks scripts
- the service is still holding the binary
- another controller process is still running

Run the script with bypass if needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-KrakenSupervisorService.ps1
```

## CAM Took Back Control

- stop CAM
- remove CAM from startup
- stop custom Kraken processes
- restart the Kraken supervisor service
