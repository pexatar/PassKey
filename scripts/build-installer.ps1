<#
.SYNOPSIS
    Builds the PassKey installer package.

.DESCRIPTION
    1. Publishes PassKey.Desktop (self-contained, x64, Release)
    2. Publishes PassKey.BrowserHost (single-file, self-contained, x64)
    3. Generates the Native Messaging Host JSON manifest
    4. Compiles the Inno Setup installer
    5. Creates a portable ZIP archive

.NOTES
    Prerequisites:
    - .NET 10 SDK
    - Inno Setup 6 (default install path)
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== PassKey Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Output: $OutputDir"
Write-Host ""

# Clean previous output
$publishPath = Join-Path $RepoRoot $OutputDir
if (Test-Path $publishPath) {
    Write-Host "Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $publishPath
}

# 1. Publish Desktop
Write-Host "Publishing PassKey.Desktop..." -ForegroundColor Green
dotnet publish "$RepoRoot\src\PassKey.Desktop\PassKey.Desktop.csproj" `
    -c $Configuration -p:Platform=x64 -r win-x64 `
    --self-contained true `
    -o $publishPath
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed" }

# 2. Publish BrowserHost (single file, into same output folder)
Write-Host "Publishing PassKey.BrowserHost..." -ForegroundColor Green
dotnet publish "$RepoRoot\src\PassKey.BrowserHost\PassKey.BrowserHost.csproj" `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -o $publishPath
if ($LASTEXITCODE -ne 0) { throw "BrowserHost publish failed" }

# 3. Generate Native Messaging Host manifest (reference copy bundled with installer)
# NOTE: The authoritative manifests are generated at runtime by NativeMessagingRegistrationService
# into %LOCALAPPDATA%\PassKey\native-messaging\ with correct absolute paths.
# Chrome and Firefox need separate manifests (different field names), so two files are created.
Write-Host "Generating NMH manifests..." -ForegroundColor Green

$chromeManifest = @{
    name = "com.passkey.host"
    description = "PassKey Native Messaging Host"
    path = "PassKey.BrowserHost.exe"
    type = "stdio"
    allowed_origins = @(
        "chrome-extension://jmddfinmjgpgmfkiblhnjccagheadpop/"
    )
} | ConvertTo-Json -Depth 3

$firefoxManifest = @{
    name = "com.passkey.host"
    description = "PassKey Native Messaging Host"
    path = "PassKey.BrowserHost.exe"
    type = "stdio"
    allowed_extensions = @(
        "{3E08FACC-D43B-4B20-89E7-7888F6082E9D}"
    )
} | ConvertTo-Json -Depth 3

$chromeManifest  | Out-File -Encoding utf8 -FilePath (Join-Path $publishPath "com.passkey.host.json")
$firefoxManifest | Out-File -Encoding utf8 -FilePath (Join-Path $publishPath "com.passkey.host.firefox.json")

# 4. Compile Inno Setup installer
if (Test-Path $InnoSetupPath) {
    Write-Host "Compiling Inno Setup installer..." -ForegroundColor Green
    & $InnoSetupPath "$RepoRoot\Installer\PassKey.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }
    Write-Host "Installer created: Installer\Output\PassKey-Setup-x64.exe" -ForegroundColor Cyan
} else {
    Write-Host "Inno Setup not found at $InnoSetupPath — skipping installer" -ForegroundColor Yellow
}

# 5. Create portable ZIP
Write-Host "Creating portable ZIP..." -ForegroundColor Green
$zipPath = Join-Path $RepoRoot "Installer\Output\PassKey-Portable-x64.zip"
$zipDir = Split-Path $zipPath
if (-not (Test-Path $zipDir)) { New-Item -ItemType Directory -Path $zipDir | Out-Null }
Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
Write-Host "Portable ZIP created: $zipPath" -ForegroundColor Cyan

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Cyan
