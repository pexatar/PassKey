param(
    [string]$Arch   = "x64",       # x64 | arm64
    [string]$Config = "Release",
    [string]$OutDir = ""
)

if ($OutDir -eq "") { $OutDir = "dist\$Arch" }

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== PassKey Publish — $Arch $Config ===" -ForegroundColor Cyan

# 1. Publish BrowserHost (self-contained, single file)
Write-Host "`n[1/2] Publishing BrowserHost..." -ForegroundColor Yellow
dotnet publish "$root\src\PassKey.BrowserHost\PassKey.BrowserHost.csproj" `
    -c $Config -r "win-$Arch" --self-contained true -p:PublishSingleFile=true `
    -o "$root\$OutDir" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "BrowserHost publish failed (exit $LASTEXITCODE)" }

# 2. Publish Desktop (self-contained: .NET runtime is bundled in the output folder).
#    WindowsAppSDKSelfContained is NOT set, so Microsoft.UI.Xaml.dll and other
#    Windows App Runtime DLLs are NOT bundled — they are loaded at runtime from the
#    system-installed Windows App Runtime 1.8 via Bootstrap.Initialize().
#    This avoids both the STATUS_INVALID_IMAGE_HASH (NuGet DLLs rejected by HVCI)
#    and the .NET-not-found startup failure caused by a silent .NET installer failure.
Write-Host "`n[2/2] Publishing Desktop..." -ForegroundColor Yellow
$platformArg = if ($Arch -eq "arm64") { "ARM64" } else { "x64" }
dotnet publish "$root\src\PassKey.Desktop\PassKey.Desktop.csproj" `
    -c $Config -r "win-$Arch" --self-contained true -p:Platform=$platformArg `
    -o "$root\$OutDir" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed (exit $LASTEXITCODE)" }

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Output: $root\$OutDir"
Get-ChildItem "$root\$OutDir\PassKey*" -ErrorAction SilentlyContinue |
    Select-Object Name, @{N="Size (KB)"; E={[math]::Round($_.Length/1KB,1)}} |
    Format-Table -AutoSize
