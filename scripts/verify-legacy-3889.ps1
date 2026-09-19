[CmdletBinding()]
param(
    [string]$GtaDir = 'C:\Program Files\9d2d0eb64d5c44529cece33fe2a46482',
    [string]$Profile = ''
)

$ErrorActionPreference = 'Stop'
$Profile = if ($Profile) { $Profile } else { Join-Path $PSScriptRoot '..\runtime\compat\legacy-3889\native-profile.json' }
$profileData = Get-Content -LiteralPath $Profile -Raw | ConvertFrom-Json
$required = @('id', 'gameExecutable', 'gameFileVersion', 'gameSize', 'gameSha256', 'updateRpfSha256', 'update2RpfSha256', 'supportStatus')
$missing = @($required | Where-Object { -not ($profileData.PSObject.Properties.Name -contains $_) -or [string]::IsNullOrWhiteSpace([string]$profileData.$_) })
if ($missing.Count -gt 0) {
    throw "Профиль совместимости неполный ($Profile): отсутствуют $($missing -join ', '). Используйте runtime/compat/legacy-3889/native-profile.json."
}
if (-not $profileData.nativeClient -or [string]::IsNullOrWhiteSpace([string]$profileData.nativeClient.requiredAdapter)) {
    throw "Профиль совместимости неполный ($Profile): отсутствует nativeClient.requiredAdapter."
}
$exe = Join-Path $GtaDir $profileData.gameExecutable
$issues = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    $issues.Add("GTA5.exe не найден: $exe")
} else {
    $item = Get-Item -LiteralPath $exe
    $version = $item.VersionInfo.FileVersion.Trim()
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()

    if ($version -ne $profileData.gameFileVersion) { $issues.Add("Версия: ожидалась $($profileData.gameFileVersion), получена $version") }
    if ($item.Length -ne [int64]$profileData.gameSize) { $issues.Add("Размер GTA5.exe не совпал") }
    if ($hash -ne $profileData.gameSha256) { $issues.Add("SHA-256 GTA5.exe не совпал") }

    foreach ($rpf in @(
        @{ Relative = 'update\update.rpf'; Expected = $profileData.updateRpfSha256 },
        @{ Relative = 'update\update2.rpf'; Expected = $profileData.update2RpfSha256 }
    )) {
        $path = Join-Path $GtaDir $rpf.Relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $issues.Add("Не найден $($rpf.Relative)")
        } elseif ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $rpf.Expected) {
            $issues.Add("SHA-256 $($rpf.Relative) не совпал")
        }
    }
}

[pscustomobject]@{
    Profile = $profileData.id
    Game = $profileData.gameFileVersion
    FilesMatch = ($issues.Count -eq 0)
    NativeAdapter = $profileData.nativeClient.requiredAdapter
    NativeStatus = $profileData.nativeClient.status
    SupportStatus = $profileData.supportStatus
    Issues = @($issues)
} | ConvertTo-Json -Depth 5

if ($issues.Count -gt 0 -or $profileData.supportStatus -ne 'supported') { exit 2 }
