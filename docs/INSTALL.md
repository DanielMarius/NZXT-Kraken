# Install Guide

## Prerequisites

- Windows machine with the supported NZXT Kraken LCD cooler attached
- .NET 7 SDK installed
- Administrative PowerShell session for service installation
- NZXT CAM not actively controlling the same device

## Manual Validation First

1. Build:

```powershell
dotnet build .\KrakenHost.csproj -c Release
```

2. Run manually:

```powershell
.\bin\Release\net7.0-windows\KrakenHost.exe --interval=2 --min-lcd-push=2 --max-lcd-refresh=30
```

3. Confirm:

- LCD renders correctly
- fan and pump respond
- `runtime\health.json` updates
- controller log stays clean

## Install The Supervisor Service

```powershell
.\Install-KrakenSupervisorService.ps1
```

If the machine blocks local scripts:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-KrakenSupervisorService.ps1
```

## Verify Installation

```powershell
Get-Service KrakenSupervisor
Get-Process KrakenHost
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
```

Expected:

- service status is `Running`
- one supervisor process
- one controller process
- fresh health payload with `Status` equal to `ok`

## Uninstall

```powershell
.\Uninstall-KrakenSupervisorService.ps1
```
