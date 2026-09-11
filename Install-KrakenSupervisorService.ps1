param(
    [switch]$CoolingFallbackVerified,
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"

$serviceName = "KrakenSupervisor"
$displayName = "Kraken Supervisor"
$root = $PSScriptRoot
$project = Join-Path $root "KrakenHost.csproj"

if (!(Test-Path $project)) {
    throw "Missing project file: $project"
}

# Never turn an installation command into an unattended cooling interruption.
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    throw "Existing Kraken service preserved. Stage and test separately; a supervised cooling handoff is required for upgrades."
}
if (Get-Process -Name KrakenHost -ErrorAction SilentlyContinue) {
    throw "A Kraken process is already running. No process will be stopped by this installer."
}
if (!$CoolingFallbackVerified) {
    throw "Fresh installation requires -CoolingFallbackVerified after independently verifying fan/pump control during setup."
}

function Invoke-HiddenChecked([string]$FileName, [string]$Arguments, [int]$TimeoutMilliseconds = 60000) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $FileName
    $start.Arguments = $Arguments
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutMilliseconds)) {
            # Only this helper's own child is eligible for termination. Never
            # search for or stop a compiler/controller by name.
            if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
            throw "Timed out running $FileName; installation has not continued."
        }
        Write-Output $stdout.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw $stderr.GetAwaiter().GetResult() }
    } finally { $process.Dispose() }
}

$dotnet = (Get-Command -Name $DotnetPath -CommandType Application -ErrorAction Stop).Source
$version = "net10.0-windows-win-x64-{0}-{1}" -f [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ"), [Guid]::NewGuid().ToString("N")
$stageRoot = Join-Path $root "publish\$version"
$publishRoot = Join-Path $stageRoot "app"
if (Test-Path $stageRoot) {
    throw "Publish directory already exists; no existing installation will be overwritten: $stageRoot"
}
New-Item -ItemType Directory -Path $stageRoot -ErrorAction Stop | Out-Null
# Keep publish output and intermediates separate from every existing runtime.
Invoke-HiddenChecked $dotnet "publish `"$project`" -c Release -f net10.0-windows -r win-x64 --self-contained true --output `"$publishRoot`" -p:BaseOutputPath=`"$stageRoot/bin/`" -p:BaseIntermediateOutputPath=`"$stageRoot/obj/`" -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseSharedCompilation=false -nodeReuse:false" 300000

$exe = Join-Path $publishRoot "KrakenHost.exe"

if (!(Test-Path $exe)) {
    throw "Missing executable: $exe"
}

Invoke-HiddenChecked $exe "--self-test"
if ((Get-Service -Name $serviceName -ErrorAction SilentlyContinue) -or
    (Get-Process -Name KrakenHost -ErrorAction SilentlyContinue)) {
    throw "A Kraken service or process appeared during validation. Existing cooling is preserved; installation has not continued."
}

Invoke-HiddenChecked "$env:SystemRoot\System32\sc.exe" "create $serviceName binPath= `"\`"$exe\`" --windows-service`" start= auto DisplayName= `"$displayName`""
Invoke-HiddenChecked "$env:SystemRoot\System32\sc.exe" "description $serviceName `"Supervises owned Kraken cooling; live unhealthy recovery requires verified cooling fallback.`""
Invoke-HiddenChecked "$env:SystemRoot\System32\sc.exe" "failure $serviceName reset= 0 actions= restart/5000/restart/5000/restart/5000"
Invoke-HiddenChecked "$env:SystemRoot\System32\sc.exe" "start $serviceName"

Write-Host "Installed Kraken supervisor Windows service from $publishRoot."
