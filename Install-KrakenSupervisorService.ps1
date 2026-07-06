$ErrorActionPreference = "Stop"

$serviceName = "KrakenSupervisor"
$displayName = "Kraken Supervisor"
$root = $PSScriptRoot
$project = Join-Path $root "KrakenHost.csproj"

if (!(Test-Path $project)) {
    throw "Missing project file: $project"
}

sc.exe stop $serviceName | Out-Null
Start-Sleep -Seconds 2
Get-Process KrakenHost -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
sc.exe delete $serviceName | Out-Null
Start-Sleep -Seconds 2

dotnet build $project -c Release | Out-Host

$exe = Join-Path $root "bin\Release\net7.0-windows\KrakenHost.exe"
$binPath = "`"$exe`" --windows-service"

if (!(Test-Path $exe)) {
    throw "Missing executable: $exe"
}

sc.exe create $serviceName binPath= $binPath start= auto DisplayName= $displayName | Out-Null
sc.exe description $serviceName "Starts and supervises the Kraken C# controller. Verifies health every 10 seconds and restarts on integrity failure." | Out-Null
sc.exe failure $serviceName reset= 0 actions= restart/5000/restart/5000/restart/5000 | Out-Null
sc.exe start $serviceName | Out-Null

Write-Host "Installed Kraken supervisor Windows service from $root."
