<#
.SYNOPSIS
    Assembles an alt:V client "core" folder into <repo>\runtime\client and
    reports which files are still missing (measured against the client's
    own update.json manifest).

.DESCRIPTION
    The backup at C:\ViMP backup\backup-altv\client is INCOMPLETE - as of the
    2026-08-27 recon it had 17 of 85 files; the CEF locale paks, V8 snapshots,
    crypto DLLs, legacy.dll etc. are absent. Those normally come from the
    (now dead) alt:V CDN. Until the full 16.4.39 client file set is sourced,
    the game cannot be launched.

    This script:
      1. copies everything present in the backup client folder;
      2. if -FillFrom is given, pulls any still-missing files from there
         (e.g. a complete alt:V install / another backup / a CDN mirror);
      3. prints present/missing counts and writes runtime\client\MISSING.txt.

    ASCII-only (Windows PowerShell 5.1 codepage parsing).

.PARAMETER AltvBackup
    Root of the alt:V backup. Default C:\ViMP backup\backup-altv.

.PARAMETER Branch
    release | rc | dev. Default release.

.PARAMETER OutDir
    Target. Default <repo>\runtime\client.

.PARAMETER FillFrom
    Optional folder holding the missing client files (same relative layout,
    e.g. libs\resources.pak, cef\locales\en-US.pak).

.EXAMPLE
    powershell -File scripts/assemble-client-core.ps1
    powershell -File scripts/assemble-client-core.ps1 -FillFrom "D:\altv-full\release"
#>
[CmdletBinding()]
param(
    [string]$AltvBackup = "C:\ViMP backup\backup-altv",
    [ValidateSet("release", "rc", "dev")]
    [string]$Branch     = "release",
    [string]$OutDir     = "",
    [string]$FillFrom   = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$repo = Split-Path $scriptDir -Parent
if (-not $OutDir) { $OutDir = Join-Path $repo "runtime\client" }

$src = Join-Path $AltvBackup "client\$Branch\x64_win32"
if (-not (Test-Path $src)) { throw "Client source not found: $src" }
$manifestPath = Join-Path $src "update.json"
if (-not (Test-Path $manifestPath)) { throw "update.json not found: $manifestPath" }

Write-Host "== FloV:MP assemble-client-core ==" -ForegroundColor Cyan
Write-Host "src     : $src"
Write-Host "out     : $OutDir"
if ($FillFrom) { Write-Host "fill    : $FillFrom" }
Write-Host ""

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$expected = @($manifest.hashList.PSObject.Properties.Name)

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Copy-Rel {
    param([string]$Root, [string]$Rel)
    $from = Join-Path $Root ($Rel -replace '/', '\')
    if (-not (Test-Path $from)) { return $false }
    $to = Join-Path $OutDir ($Rel -replace '/', '\')
    $dir = Split-Path $to -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -LiteralPath $from -Destination $to -Force
    return $true
}

# update.json itself
Copy-Item $manifestPath (Join-Path $OutDir "update.json") -Force

$fromBackup = 0; $fromFill = 0; $missing = @()
foreach ($rel in $expected) {
    if (Copy-Rel $src $rel) { $fromBackup++; continue }
    if ($FillFrom -and (Copy-Rel $FillFrom $rel)) { $fromFill++; continue }
    $missing += $rel
}

$missingPath = Join-Path $OutDir "MISSING.txt"
if ($missing.Count -gt 0) {
    $missing | Set-Content $missingPath -Encoding UTF8
} elseif (Test-Path $missingPath) {
    Remove-Item $missingPath
}

Write-Host "expected (manifest) : $($expected.Count)"
Write-Host "copied from backup  : $fromBackup" -ForegroundColor Green
if ($FillFrom) { Write-Host "copied from -FillFrom: $fromFill" -ForegroundColor Green }
Write-Host "STILL MISSING       : $($missing.Count)" -ForegroundColor $(if ($missing.Count) { "Red" } else { "Green" })

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing list written to: $missingPath" -ForegroundColor Yellow
    Write-Host "The client will NOT launch the game until these are supplied." -ForegroundColor Yellow
    Write-Host "Provide a complete alt:V $Branch client set via -FillFrom and re-run."
    $missing | Select-Object -First 20 | ForEach-Object { Write-Host "  - $_" }
    if ($missing.Count -gt 20) { Write-Host "  ... (+$($missing.Count - 20) more, see MISSING.txt)" }
    exit 2
}

Write-Host ""
Write-Host "== Client core complete: $OutDir ==" -ForegroundColor Green
