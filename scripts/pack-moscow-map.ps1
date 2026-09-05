[CmdletBinding()]
param(
    [string]$SourceMapDir = "C:\Users\User\Downloads\RMRP MAP 2025\mods",
    [string]$OutZip       = "C:\FloV-MP\dist\cdn\moscow_map.zip",
    [string]$StagingDir   = "C:\FloV-MP\dist\staging"
)

$ErrorActionPreference = "Stop"

Write-Host "== Packaging Moscow Map (Phase 1 Core) ==" -ForegroundColor Cyan
Write-Host "Source:  $SourceMapDir"
Write-Host "OutZip:  $OutZip"
Write-Host "Staging: $StagingDir"

if (-not (Test-Path $SourceMapDir)) {
    throw "Source map directory not found: $SourceMapDir"
}

$cdnDir = Split-Path $OutZip -Parent
if (-not (Test-Path $cdnDir)) { New-Item -ItemType Directory -Path $cdnDir -Force | Out-Null }
if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

$stagedMods = Join-Path $StagingDir "mods"
New-Item -ItemType Directory -Path $stagedMods -Force | Out-Null

Write-Host "[1/3] Hardlinking core Moscow map files (excluding rmrp_clothes_*)..." -ForegroundColor Yellow

$files = Get-ChildItem $SourceMapDir -Recurse -File | Where-Object { $_.FullName -notmatch "rmrp_clothes_" }
$totalFiles = $files.Count
$linked = 0

foreach ($file in $files) {
    $rel = $file.FullName.Substring($SourceMapDir.Length).TrimStart('\', '/')
    $dest = Join-Path $stagedMods $rel
    $parentDir = Split-Path $dest -Parent
    if (-not (Test-Path $parentDir)) {
        New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    }
    New-Item -ItemType HardLink -Path $dest -Value $file.FullName | Out-Null
    $linked++
}
Write-Host "  Linked $linked of $totalFiles files in staging." -ForegroundColor Green

Write-Host "[2/3] Generating manifest-map.json..." -ForegroundColor Yellow
$manifestPath = Join-Path $cdnDir "manifest-map.json"
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "make-manifest.ps1") `
    -SourceDir $stagedMods `
    -OutFile $manifestPath `
    -Version "moscow-1.0.0" `
    -BaseUrl "http://188.127.229.224/cdn"

Write-Host "[3/3] Compressing moscow_map.zip..." -ForegroundColor Cyan
if (Test-Path $OutZip) { Remove-Item $OutZip -Force }

$sw = [System.Diagnostics.Stopwatch]::StartNew()
& tar -acf $OutZip -C $StagingDir mods
$sw.Stop()

if ($LASTEXITCODE -ne 0) {
    throw "tar command failed with exit code $LASTEXITCODE"
}

$zipItem = Get-Item $OutZip
$sizeGB = [math]::Round($zipItem.Length / 1GB, 2)
Write-Host "Archive created: $OutZip ($sizeGB GB in $($sw.Elapsed.TotalSeconds.ToString('F1'))s)" -ForegroundColor Green

Write-Host "Cleaning up staging hardlinks..." -ForegroundColor Yellow
Remove-Item $StagingDir -Recurse -Force

Write-Host "== Moscow Map Packaging Complete! ==" -ForegroundColor Green
