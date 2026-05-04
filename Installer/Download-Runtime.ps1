# Download-Runtime.ps1
# Downloads the Windows App Runtime 1.8.260101001 redistributable required to
# build the PassKey installer. The file is excluded from git (> 100 MB limit).
#
# Usage: .\Installer\Download-Runtime.ps1
# Run once before building the installer with Inno Setup.

$url    = "https://aka.ms/windowsappsdk/1.8/1.8.260101001/windowsappruntimeinstall-x64.exe"
$output = Join-Path $PSScriptRoot "WindowsAppRuntimeInstall-x64.exe"

if (Test-Path $output) {
    Write-Host "Already present: $output" -ForegroundColor Green
    exit 0
}

Write-Host "Downloading Windows App Runtime 1.8.260101001..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $output -UseBasicParsing
$sizeMB = [math]::Round((Get-Item $output).Length / 1MB, 1)
Write-Host "Done — $sizeMB MB saved to: $output" -ForegroundColor Green
