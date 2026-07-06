$ErrorActionPreference = "SilentlyContinue"

Set-Service -Name CAMService -StartupType Manual
Start-Service -Name CAMService

$camExe = "C:\Program Files\NZXT CAM\NZXT CAM.exe"
if (Test-Path $camExe) {
    Start-Process -FilePath $camExe
}

Write-Host "CAM restore attempted."
