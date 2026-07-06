$nativePrefix = "Drakula KrakenNative"

schtasks /Delete /TN "$nativePrefix Startup" /F 2>$null | Out-Null
schtasks /Delete /TN "$nativePrefix Logon" /F 2>$null | Out-Null
schtasks /Delete /TN "$nativePrefix Repair" /F 2>$null | Out-Null
schtasks /Delete /TN "$nativePrefix Watchdog" /F 2>$null | Out-Null

Write-Host "Removed native Kraken scheduled tasks."
