$ErrorActionPreference = "SilentlyContinue"

$serviceName = "KrakenSupervisor"

sc.exe stop $serviceName | Out-Null
Start-Sleep -Seconds 2
sc.exe delete $serviceName | Out-Null

Write-Host "Removed Kraken supervisor Windows service."
