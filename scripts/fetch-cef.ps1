<#
.SYNOPSIS
    Downloads the official CEF build matching alt:V's client and lays its
    redistributables out in the alt:V client folder structure.

.DESCRIPTION
    alt:V 16.4.39's client bundles CEF for Chromium 131.0.6778.205
    (determined from chrome_elf.dll in the backup). The alt:V CDN is dead,
    but the CEF build server (cef-builds.spotifycdn.com) is alive and hosts
    exactly this build. Most of the client's missing files are vanilla CEF
    redistributables (paks, V8 snapshots, ANGLE/SwiftShader DLLs, locale
    paks) that are keyed to the Chromium version, not to alt:V's patches.

    This script downloads the CEF "minimal" distribution (~190 MB), extracts
    it, and copies Release/ + Resources/ files into -OutDir using the alt:V
    layout (libs\*, cef\locales\*). Feed the result to
    scripts\assemble-client-core.ps1 -FillFrom <OutDir>.

    NOT covered here (get separately, see docs/client-recovery-plan.md):
      legacy.dll (alt:V proprietary), libcrypto-3-x64.dll / libssl-3-x64.dll
      (OpenSSL 3), bassmix.dll (un4seen), discord_game_sdk.dll (Discord),
      freetype.dll.

    ASCII-only (Windows PowerShell 5.1 codepage parsing).

.PARAMETER CefVersion
    Full CEF version string. Default matches alt:V 16.4.39.

.PARAMETER Distribution
    minimal | standard. Default minimal.

.PARAMETER OutDir
    Where to write the alt:V-layout files. Default <repo>\runtime\client-fill.

.PARAMETER KeepArchive
    Keep the downloaded .tar.bz2 (default: delete after extract).

.EXAMPLE
    powershell -File scripts/fetch-cef.ps1
    powershell -File scripts/assemble-client-core.ps1 -FillFrom runtime\client-fill
#>
[CmdletBinding()]
param(
    [string]$CefVersion   = "131.3.5+g573cec5+chromium-131.0.6778.205",
    [ValidateSet("minimal", "standard")]
    [string]$Distribution = "minimal",
    [string]$OutDir       = "",
    [switch]$KeepArchive
)

$ErrorActionPreference = "Stop"
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$repo = Split-Path $scriptDir -Parent
if (-not $OutDir) { $OutDir = Join-Path $repo "runtime\client-fill" }

$archiveName = "cef_binary_${CefVersion}_windows64_${Distribution}.tar.bz2"
$urlName = $archiveName -replace '\+', '%2B'
$url = "https://cef-builds.spotifycdn.com/$urlName"

$work = Join-Path $env:TEMP ("flovmp-cef-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$archivePath = Join-Path $work $archiveName

Write-Host "== fetch-cef ==" -ForegroundColor Cyan
Write-Host "cef     : $CefVersion ($Distribution)"
Write-Host "url     : $url"
Write-Host "out     : $OutDir"
Write-Host ""

Write-Host "[download] $archiveName ..." -ForegroundColor Yellow
$ProgressPreference = "SilentlyContinue"
Invoke-WebRequest -Uri $url -OutFile $archivePath -TimeoutSec 600 -Headers @{ "User-Agent" = "Mozilla/5.0" }
$sizeMB = [math]::Round((Get-Item $archivePath).Length / 1MB, 1)
Write-Host "  got $sizeMB MB"

Write-Host "[extract] tar -xf ..." -ForegroundColor Yellow
& tar -xf $archivePath -C $work
if ($LASTEXITCODE -ne 0) { throw "tar extract failed ($LASTEXITCODE)" }

$cefRoot = Get-ChildItem $work -Directory | Where-Object { $_.Name -like "cef_binary_*" } | Select-Object -First 1
if (-not $cefRoot) { throw "extracted CEF root folder not found in $work" }
$rel = Join-Path $cefRoot.FullName "Release"
$res = Join-Path $cefRoot.FullName "Resources"

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $OutDir "libs") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutDir "cef\locales") -Force | Out-Null

$copied = 0
function Put {
    param([string]$From, [string]$ToRel)
    if (-not (Test-Path $From)) { Write-Host "  - skip (not in CEF): $ToRel"; return }
    $to = Join-Path $OutDir $ToRel
    New-Item -ItemType Directory -Path (Split-Path $to -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $From -Destination $to -Force
    Write-Host "  + $ToRel"
    $script:copied++
}

Write-Host "[map] Release -> libs" -ForegroundColor Yellow
# alt:V берёт libcef.dll как свой пропатченный libce2.dll и свой chrome_elf.dll -
# их НЕ подменяем. Остальные redistributable DLL/бины - из CEF.
foreach ($f in @(
    "d3dcompiler_47.dll", "libEGL.dll", "libGLESv2.dll",
    "vk_swiftshader.dll", "vk_swiftshader_icd.json", "vulkan-1.dll",
    "snapshot_blob.bin", "v8_context_snapshot.bin",
    "dxcompiler.dll", "dxil.dll")) {
    Put (Join-Path $rel $f) "libs/$f"
}

Write-Host "[map] Resources -> libs" -ForegroundColor Yellow
foreach ($f in @("resources.pak", "chrome_100_percent.pak", "chrome_200_percent.pak", "icudtl.dat")) {
    Put (Join-Path $res $f) "libs/$f"
}
# alt:V ждёт ещё icudtl_v8.dat - в CEF такого нет. Пробуем как копию icudtl.dat
# (best-effort, помечено как неточное в docs/client-recovery-plan.md).
if (Test-Path (Join-Path $res "icudtl.dat")) {
    Copy-Item (Join-Path $res "icudtl.dat") (Join-Path $OutDir "libs\icudtl_v8.dat") -Force
    Write-Host "  ~ libs/icudtl_v8.dat (guessed = icudtl.dat, VERIFY)"
    $copied++
}

Write-Host "[map] Resources/locales -> cef/locales" -ForegroundColor Yellow
$locSrc = Join-Path $res "locales"
if (Test-Path $locSrc) {
    Get-ChildItem "$locSrc\*.pak" | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $OutDir "cef\locales\$($_.Name)") -Force
        $copied++
    }
    Write-Host "  + cef/locales/*.pak ($((Get-ChildItem "$locSrc\*.pak").Count) files)"
}

if (-not $KeepArchive) { Remove-Item $work -Recurse -Force }
else { Write-Host "archive kept: $archivePath" }

Write-Host ""
Write-Host "== done: $copied files into $OutDir ==" -ForegroundColor Green
Write-Host "next: powershell -File scripts/assemble-client-core.ps1 -FillFrom `"$OutDir`""
