$ErrorActionPreference = "SilentlyContinue"

$patterns = @(
    "KrakenHost.exe",
    "KrakenHost.dll",
    "kraken_direct.py",
    "kraken_watchdog.py"
)

sc.exe stop KrakenSupervisor | Out-Null

Get-CimInstance Win32_Process | Where-Object {
    $cmd = [string]$_.CommandLine
    foreach ($pattern in $patterns) {
        if ($_.Name -like $pattern -or $cmd -like "*$pattern*") {
            return $true
        }
    }
    return $false
} | ForEach-Object {
    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
}

Write-Host "Stopped custom Kraken processes."
