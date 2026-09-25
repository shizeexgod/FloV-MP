<#
.SYNOPSIS
    CEF SDK для хоста браузеров клиента (flovmp-cef.exe, пункт 26b).

.DESCRIPTION
    Скачивает официальную сборку CEF (minimal, ~190 MB) с cef-builds.spotifycdn.com,
    распаковывает в .work\cef\cef131 и собирает libcef_dll_wrapper (Release, /MT).
    После этого native\legacy-3889\client собирается вместе с flovmp-cef.exe.
    Папка .work в git не попадает.

    ASCII-only (Windows PowerShell 5.1 codepage parsing).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\fetch-cef-sdk.ps1
#>
param(
    [string]$CefVersion = "131.3.5+g573cec5+chromium-131.0.6778.205"
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repo = Split-Path -Parent $PSScriptRoot
$work = Join-Path $repo '.work\cef'
$sdk = Join-Path $work 'cef131'
New-Item -ItemType Directory -Force $work | Out-Null

if (-not (Test-Path (Join-Path $sdk 'include\cef_app.h'))) {
    $name = "cef_binary_${CefVersion}_windows64_minimal.tar.bz2"
    $url = 'https://cef-builds.spotifycdn.com/' + ($name -replace '\+', '%2B')
    $archive = Join-Path $work 'cef.tar.bz2'
    Write-Host "download: $url"
    Invoke-WebRequest -Uri $url -OutFile $archive -TimeoutSec 1800 -Headers @{ 'User-Agent' = 'Mozilla/5.0' }
    & tar -xf $archive -C $work
    if ($LASTEXITCODE -ne 0) { throw "tar failed ($LASTEXITCODE)" }
    Remove-Item $archive -Force
    $extracted = Get-ChildItem $work -Directory -Filter 'cef_binary_*' | Select-Object -First 1
    if (-not $extracted) { throw 'CEF archive layout not recognized' }
    if (Test-Path $sdk) { Remove-Item $sdk -Recurse -Force }
    Rename-Item $extracted.FullName 'cef131'
}

$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if (-not $cmake) {
    $cmake = Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter cmake.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $cmake) { throw 'cmake not found (Visual Studio Build Tools)' }

$build = Join-Path $work 'build'
& $cmake -S $sdk -B $build -A x64
if ($LASTEXITCODE -ne 0) { throw 'cmake configure failed' }
& $cmake --build $build --config Release --target libcef_dll_wrapper
if ($LASTEXITCODE -ne 0) { throw 'libcef_dll_wrapper build failed' }
Write-Host "OK: $sdk + $build\libcef_dll_wrapper\Release\libcef_dll_wrapper.lib"
