# Download-Runtime.ps1
# Downloads the redistributables required to build the PassKey installer.
# Both files are excluded from git (> 100 MB for WindowsAppRuntime, consistency for .NET).
#
# Usage: .\Installer\Download-Runtime.ps1
# Run once before building the installer with Inno Setup.

$files = @(
    @{
        Url    = "https://aka.ms/windowsappsdk/1.8/1.8.260416003/windowsappruntimeinstall-x64.exe"
        Output = Join-Path $PSScriptRoot "WindowsAppRuntimeInstall-x64.exe"
        Label  = "Windows App Runtime 1.8.7 (1.8.260416003)"
    }
    # Note: .NET runtime is no longer needed here.
    # PassKey.Desktop is published self-contained — the .NET runtime is bundled
    # in the application folder, so end users do not need .NET installed.
)

foreach ($f in $files) {
    if (Test-Path $f.Output) {
        Write-Host "Already present: $($f.Label)" -ForegroundColor Green
        continue
    }
    Write-Host "Downloading $($f.Label)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $f.Url -OutFile $f.Output -UseBasicParsing
    $sizeMB = [math]::Round((Get-Item $f.Output).Length / 1MB, 1)
    Write-Host "Done — $sizeMB MB saved to: $($f.Output)" -ForegroundColor Green
}
