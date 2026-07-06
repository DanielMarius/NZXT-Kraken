$ErrorActionPreference = "SilentlyContinue"

Stop-ScheduledTask -TaskName "Drakula KrakenNative Watchdog" -ErrorAction SilentlyContinue | Out-Null

Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "KrakenHost.exe"
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Write-Output "Stopped KrakenHost native watchdog and service instances."
