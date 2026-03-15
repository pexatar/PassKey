#Requires -Version 5.1
<#
.SYNOPSIS
    Registers PassKey Native Messaging Host for Chrome and Firefox.

.DESCRIPTION
    Creates Windows Registry entries that allow Chrome and Firefox to discover
    the PassKey BrowserHost executable via Native Messaging protocol.

    Registry keys:
      Chrome:  HKCU:\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.passkey.host
      Firefox: HKCU:\SOFTWARE\Mozilla\NativeMessagingHosts\com.passkey.host

    Each key's (Default) value points to the corresponding JSON manifest file.

.PARAMETER Unregister
    If specified, removes the registry keys instead of creating them.

.EXAMPLE
    .\register-native-host.ps1
    .\register-native-host.ps1 -Unregister
#>

[CmdletBinding()]
param(
    [switch]$Unregister
)

$ErrorActionPreference = 'Stop'

# Resolve paths relative to this script's parent (project root/scripts/)
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$manifestDir = Join-Path $projectRoot 'src\PassKey.BrowserHost\Manifests'

$chromeManifest  = Join-Path $manifestDir 'com.passkey.host.json'
$firefoxManifest = Join-Path $manifestDir 'com.passkey.host.firefox.json'

$chromeRegKey  = 'HKCU:\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.passkey.host'
$firefoxRegKey = 'HKCU:\SOFTWARE\Mozilla\NativeMessagingHosts\com.passkey.host'

# Also register for Edge (Chromium-based, uses same Chrome key path)
$edgeRegKey = 'HKCU:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\com.passkey.host'

function Register-Host {
    param(
        [string]$BrowserName,
        [string]$RegistryPath,
        [string]$ManifestPath
    )

    if (-not (Test-Path $ManifestPath)) {
        Write-Warning "  Manifest not found: $ManifestPath - skipping $BrowserName"
        return
    }

    # Ensure parent key exists
    $parentPath = Split-Path -Parent $RegistryPath
    if (-not (Test-Path $parentPath)) {
        New-Item -Path $parentPath -Force | Out-Null
    }

    # Create the key with (Default) pointing to the manifest
    if (-not (Test-Path $RegistryPath)) {
        New-Item -Path $RegistryPath -Force | Out-Null
    }
    Set-ItemProperty -Path $RegistryPath -Name '(Default)' -Value $ManifestPath

    Write-Host "  [OK] $BrowserName registered" -ForegroundColor Green
    Write-Host "        Key:      $RegistryPath"
    Write-Host "        Manifest: $ManifestPath"
}

function Unregister-Host {
    param(
        [string]$BrowserName,
        [string]$RegistryPath
    )

    if (Test-Path $RegistryPath) {
        Remove-Item -Path $RegistryPath -Force
        Write-Host "  [OK] $BrowserName unregistered" -ForegroundColor Yellow
    } else {
        Write-Host "  [--] $BrowserName was not registered" -ForegroundColor DarkGray
    }
}

# --- Main ---

if ($Unregister) {
    Write-Host "`nUnregistering PassKey Native Messaging Host...`n" -ForegroundColor Cyan

    Unregister-Host -BrowserName 'Chrome'  -RegistryPath $chromeRegKey
    Unregister-Host -BrowserName 'Edge'    -RegistryPath $edgeRegKey
    Unregister-Host -BrowserName 'Firefox' -RegistryPath $firefoxRegKey

    Write-Host "`nDone.`n"
} else {
    Write-Host "`nRegistering PassKey Native Messaging Host...`n" -ForegroundColor Cyan

    # Verify BrowserHost executable exists
    $exePath = Join-Path $projectRoot 'src\PassKey.BrowserHost\bin\Debug\net10.0\win-x64\PassKey.BrowserHost.exe'
    if (-not (Test-Path $exePath)) {
        Write-Warning "BrowserHost executable not found at: $exePath"
        Write-Warning "Run 'dotnet build src/PassKey.BrowserHost/PassKey.BrowserHost.csproj' first."
    } else {
        Write-Host "  BrowserHost found: $exePath" -ForegroundColor Green
    }

    Register-Host -BrowserName 'Chrome'  -RegistryPath $chromeRegKey  -ManifestPath $chromeManifest
    Register-Host -BrowserName 'Edge'    -RegistryPath $edgeRegKey    -ManifestPath $chromeManifest
    Register-Host -BrowserName 'Firefox' -RegistryPath $firefoxRegKey -ManifestPath $firefoxManifest

    Write-Host "`nDone. Restart your browser(s) for changes to take effect.`n"
}
