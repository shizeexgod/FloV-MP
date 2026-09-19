[CmdletBinding()]
param(
    [string]$GtaDir = 'C:\Program Files\Rockstar Games\Grand Theft Auto V',
    [string]$Profile = '',
    [string]$Capture = ''
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Profile = if ($Profile) { $Profile } else { Join-Path $scriptRoot '..\runtime\compat\enhanced-1158\native-profile.json' }
$profileData = Get-Content -LiteralPath $Profile -Raw | ConvertFrom-Json
$GtaDir = [IO.Path]::GetFullPath($GtaDir)
$issues = [System.Collections.Generic.List[string]]::new()

function Get-ObservedFile([string]$RelativePath) {
    $path = Join-Path $GtaDir $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [ordered]@{ path = $RelativePath; exists = $false }
    }
    $item = Get-Item -LiteralPath $path
    return [ordered]@{
        path = $RelativePath
        exists = $true
        size = $item.Length
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if ($profileData.id -ne 'enhanced-1158' -or $profileData.gameExecutable -ne 'GTA5_Enhanced.exe') {
    throw "Ожидался профиль enhanced-1158 для GTA5_Enhanced.exe: $Profile"
}
$exe = Join-Path $GtaDir $profileData.gameExecutable
$observed = [ordered]@{
    exe = Get-ObservedFile $profileData.gameExecutable
    update = Get-ObservedFile 'update\update.rpf'
    update2 = Get-ObservedFile 'update\update2.rpf'
}

if (-not $observed.exe.exists) {
    $issues.Add("GTA5_Enhanced.exe не найден: $exe")
} else {
    $item = Get-Item -LiteralPath $exe
    $version = $item.VersionInfo.FileVersion.Trim()
    $observed.exe.fileVersion = $version
    if ($version -ne $profileData.gameFileVersion) {
        $issues.Add("Версия: ожидалась $($profileData.gameFileVersion), получена $version")
    }
    if ($profileData.gameSize -ne $null -and [int64]$item.Length -ne [int64]$profileData.gameSize) {
        $issues.Add("Размер GTA5_Enhanced.exe не совпал")
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$profileData.gameSha256) -and
        $observed.exe.sha256 -ne ([string]$profileData.gameSha256).ToLowerInvariant()) {
        $issues.Add("SHA-256 GTA5_Enhanced.exe не совпал")
    }
}

foreach ($pair in @(
    @{ Key = 'updateRpfSha256'; Observed = 'update'; Label = 'update.rpf' },
    @{ Key = 'update2RpfSha256'; Observed = 'update2'; Label = 'update2.rpf' }
)) {
    $expected = [string]$profileData.($pair.Key)
    if (-not [string]::IsNullOrWhiteSpace($expected)) {
        if (-not $observed.($pair.Observed).exists) {
            $issues.Add("Не найден $($pair.Label)")
        } elseif ($observed.($pair.Observed).sha256 -ne $expected.ToLowerInvariant()) {
            $issues.Add("SHA-256 $($pair.Label) не совпал")
        }
    }
}

$fingerprintReady = (-not [string]::IsNullOrWhiteSpace([string]$profileData.gameSha256)) -and
    (-not [string]::IsNullOrWhiteSpace([string]$profileData.updateRpfSha256)) -and
    (-not [string]::IsNullOrWhiteSpace([string]$profileData.update2RpfSha256))
if (-not $fingerprintReady) {
    $issues.Add('Профиль ещё не содержит подтверждённые Windows SHA-256; текущий запуск только собирает отпечаток.')
}

$report = [ordered]@{
    schema = 1
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    profile = $profileData.id
    gtaDir = $GtaDir
    expectedVersion = $profileData.gameFileVersion
    fingerprintStatus = if ($fingerprintReady) { 'captured' } else { 'pending-windows-capture' }
    nativeAdapter = $profileData.nativeClient.requiredAdapter
    nativeStatus = $profileData.nativeClient.status
    supportStatus = $profileData.supportStatus
    filesMatch = ($issues.Count -eq 0)
    fingerprintReady = $fingerprintReady
    observed = $observed
    issues = @($issues)
    note = 'Этот скрипт не подменяет GTA/RPF и не включает поддержку; он только фиксирует отпечаток чистой Enhanced-установки для последующей проверки адаптера.'
}

$json = $report | ConvertTo-Json -Depth 8
if ($Capture) {
    $target = [IO.Path]::GetFullPath($Capture)
    $parent = Split-Path -Parent $target
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [IO.File]::WriteAllText($target, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}
$json

if ($issues.Count -gt 0 -or $profileData.supportStatus -ne 'supported' -or $profileData.nativeClient.status -ne 'ready') { exit 2 }
