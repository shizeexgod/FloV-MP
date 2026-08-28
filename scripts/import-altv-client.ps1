<#
.SYNOPSIS
    Builds runtime\client from a COMPLETE alt:V client payload folder.

.DESCRIPTION
    Unlike assemble-client-core.ps1 (which stitched a client from an
    incomplete backup + CEF + third-party DLLs), this takes one verified
    payload in the alt:V layout (altv-client.dll, altv-launcher-patcher.dll,
    altv.exe, altv.toml, cef\, libs\) and lays it out as-is, then checks
    hashes against a manifest.

    Default source: the payload from the GTAMP project (sources\payload) -
    a bit-for-bit official alt:V 16.4.39 client, sdkVersion c150769, the
    same version as the FloV:MP server. Verified 86/86 hash-match.

    ASCII-only (Windows PowerShell 5.1 codepage parsing).

.PARAMETER PayloadDir
    Folder holding a complete client (has altv-client.dll, cef\, libs\).

.PARAMETER ManifestJson
    update.json of the matching version, for the hash check.

.PARAMETER OutDir
    Target. Default <repo>\runtime\client.

.EXAMPLE
    powershell -File scripts/import-altv-client.ps1 -PayloadDir "D:\gtamp\sources\payload"
#>
[CmdletBinding()]
param(
    [string]$PayloadDir = "",
    [string]$ManifestJson = "C:\ViMP backup\backup-altv\client\release\x64_win32\update.json",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$repo = Split-Path $scriptDir -Parent
if (-not $OutDir) { $OutDir = Join-Path $repo "runtime\client" }

if (-not $PayloadDir -or -not (Test-Path $PayloadDir)) {
    throw "PayloadDir not set / not found: '$PayloadDir'"
}
foreach ($need in @("altv-client.dll", "cef", "libs")) {
    if (-not (Test-Path (Join-Path $PayloadDir $need))) {
        throw "payload has no '$need' - not a complete alt:V client"
    }
}

Write-Host "== import-altv-client ==" -ForegroundColor Cyan
Write-Host "payload : $PayloadDir"
Write-Host "out     : $OutDir"

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

foreach ($f in @("altv-client.dll", "altv-launcher-patcher.dll", "altv.exe", "altv.toml")) {
    $src = Join-Path $PayloadDir $f
    if (Test-Path $src) { Copy-Item $src $OutDir -Force }
}
Copy-Item (Join-Path $PayloadDir "cef") $OutDir -Recurse -Force
Copy-Item (Join-Path $PayloadDir "libs") $OutDir -Recurse -Force
Copy-Item $ManifestJson (Join-Path $OutDir "update.json") -Force

# skin.bin (alt:V launcher config blob) - from altv-resources next to payload, if present
$skin = Join-Path (Split-Path $PayloadDir -Parent) "altv-resources\skin.bin"
if (Test-Path $skin) {
    New-Item -ItemType Directory -Path (Join-Path $OutDir "cache") -Force | Out-Null
    Copy-Item $skin (Join-Path $OutDir "cache\skin.bin") -Force
    Write-Host "  + cache\skin.bin"
}

# --- hash check vs manifest ---
$manifest = Get-Content $ManifestJson -Raw | ConvertFrom-Json
$expected = $manifest.hashList.PSObject.Properties.Name
$match = 0; $mismatch = 0; $absent = 0; $mm = @()
foreach ($rel in $expected) {
    $file = Join-Path $OutDir ($rel -replace '/', '\')
    if (-not (Test-Path $file)) { $absent++; continue }
    $h = (Get-FileHash $file -Algorithm SHA1).Hash.ToLower()
    if ($h -eq $manifest.hashList.$rel.ToLower()) { $match++ }
    else { $mismatch++; $mm += "  $rel" }
}

$total = (Get-ChildItem $OutDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ""
Write-Host ("files: {0}  hash-match: {1}  mismatch: {2}  absent: {3}  ({4:N1} MB)" -f $expected.Count, $match, $mismatch, $absent, $total)
if ($mm.Count -gt 0) {
    Write-Host "mismatch (not bit-stock; usually still works):" -ForegroundColor Yellow
    $mm | ForEach-Object { Write-Host $_ }
}
if ($absent -gt 0) {
    Write-Host "WARNING: $absent file(s) absent" -ForegroundColor Red
    exit 2
}

Write-Host ""
Write-Host "== runtime\client ready ==" -ForegroundColor Green
Write-Host 'next: powershell -File scripts/make-manifest.ps1 -SourceDir runtime\client -OutFile runtime\client\manifest.json -Version altv-16.4.39 -BaseUrl runtime\client'

