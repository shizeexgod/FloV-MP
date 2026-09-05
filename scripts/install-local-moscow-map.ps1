[CmdletBinding()]
param(
    [string]$GtaDir = "",
    [string]$SourceMapDir = "C:\Users\User\Downloads\RMRP MAP 2025\mods",
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

if (-not $GtaDir) {
    $reg = Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V" -ErrorAction SilentlyContinue
    if ($reg -and $reg.InstallFolder -and (Test-Path $reg.InstallFolder)) {
        $GtaDir = $reg.InstallFolder
    } elseif (Test-Path "C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482\GTA5.exe") {
        $GtaDir = "C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482"
    } else {
        throw "Could not auto-detect GTA V directory. Pass -GtaDir explicitly."
    }
}

$modsDir = Join-Path $GtaDir "mods"

if ($Uninstall) {
    Write-Host "== Uninstalling Moscow Map mods from $GtaDir ==" -ForegroundColor Yellow
    if (Test-Path $modsDir) {
        Remove-Item $modsDir -Recurse -Force
        Write-Host "Removed $modsDir. GTA V restored to vanilla." -ForegroundColor Green
    } else {
        Write-Host "No mods directory found in $GtaDir."
    }
    return
}

Write-Host "== Installing Moscow Map to Local GTA V ==" -ForegroundColor Cyan
Write-Host "GTA V Dir:  $GtaDir"
Write-Host "Source Map: $SourceMapDir"
Write-Host "Target:     $modsDir"

if (-not (Test-Path $SourceMapDir)) {
    throw "Source map not found: $SourceMapDir"
}

if (-not (Test-Path $modsDir)) {
    New-Item -ItemType Directory -Path $modsDir -Force | Out-Null
}

$files = Get-ChildItem $SourceMapDir -Recurse -File | Where-Object { $_.FullName -notmatch "rmrp_clothes_" }
$count = 0

foreach ($file in $files) {
    $rel = $file.FullName.Substring($SourceMapDir.Length).TrimStart('\', '/')
    $dest = Join-Path $modsDir $rel
    $parent = Split-Path $dest -Parent
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    if (Test-Path $dest) {
        Remove-Item $dest -Force
    }
    New-Item -ItemType HardLink -Path $dest -Value $file.FullName | Out-Null
    $count++
}

Write-Host "Successfully installed Moscow Map into $modsDir ($count files linked via NTFS hardlinks)." -ForegroundColor Green
Write-Host "Zero extra disk space consumed. To uninstall, run: powershell -File scripts/install-local-moscow-map.ps1 -Uninstall" -ForegroundColor Cyan
