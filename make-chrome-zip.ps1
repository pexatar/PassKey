Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$src  = 'A:\Progetti\00 PassKey\PK4\extensions\chrome'

# Read the version from the manifest so the package name always matches the extension.
$version = (Get-Content (Join-Path $src 'manifest.json') -Raw | ConvertFrom-Json).version
$dest = "A:\Progetti\00 PassKey\PK4\passkey-chrome-$version.zip"

if (Test-Path $dest) { Remove-Item $dest -Force }

$stream = [System.IO.File]::Open($dest, [System.IO.FileMode]::Create)
$zip    = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)

Get-ChildItem -Path $src -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($src.Length + 1).Replace('\', '/')

    if ($relative -eq 'manifest.json') {
        # Strip the "key" field — Chrome Web Store rejects manifests that contain it
        $json = Get-Content $_.FullName -Raw | ConvertFrom-Json
        $json.PSObject.Properties.Remove('key')
        $content = ($json | ConvertTo-Json -Depth 10)
        $bytes   = [System.Text.Encoding]::UTF8.GetBytes($content)
        $entry   = $zip.CreateEntry($relative)
        $writer  = $entry.Open()
        $writer.Write($bytes, 0, $bytes.Length)
        $writer.Dispose()
        Write-Host "  Added: $relative  (key field stripped)"
    } else {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative) | Out-Null
        Write-Host "  Added: $relative"
    }
}

$zip.Dispose()
$stream.Dispose()

# Verify
$zip2    = [System.IO.Compression.ZipFile]::OpenRead($dest)
$entries = $zip2.Entries | ForEach-Object { $_.FullName } | Sort-Object
$zip2.Dispose()

Write-Host ""
Write-Host "ZIP contents:" -ForegroundColor Cyan
$entries | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host ("Done! Size: " + [math]::Round((Get-Item $dest).Length / 1KB, 1) + " KB") -ForegroundColor Green
