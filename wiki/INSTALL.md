# Install Guide

## Prerequisites

- Windows
- .NET 7 SDK
- NZXT Kraken LCD cooler attached
- administrator PowerShell session for service install
- NZXT CAM not actively controlling the same hardware

## Manual Test First

```powershell
dotnet build .\KrakenHost.csproj -c Release
.\bin\Release\net7.0-windows\KrakenHost.exe --interval=2 --min-lcd-push=2 --max-lcd-refresh=30
```

Confirm:

- fan and pump respond
- LCD looks correct
- `bin\Release\net7.0-windows\runtime\health.json` updates
- `bin\Release\net7.0-windows\logs\controller.log` stays clean

## Install The Service

```powershell
.\Install-KrakenSupervisorService.ps1
```

If scripts are blocked:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-KrakenSupervisorService.ps1
```

## Verify

```powershell
Get-Service KrakenSupervisor
Get-Process KrakenHost
Get-Content .\bin\Release\net7.0-windows\runtime\health.json
```
