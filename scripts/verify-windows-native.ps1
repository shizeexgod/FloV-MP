[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('legacy-3889', 'enhanced-1158')]
    [string]$Profile,
    [Parameter(Mandatory = $true)]
    [string]$GtaDir,
    [Parameter(Mandatory = $true)]
    [string]$NativeAdapterPath,
    [string]$AdapterManifestPath = '',
    [string]$ConnectorPath = '',
    [string]$E2EReport = '',
    [string]$ProfilePath = '',
    [string]$WriteReport = ''
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ProfilePath) {
    $folder = if ($Profile -eq 'legacy-3889') { 'legacy-3889' } else { 'enhanced-1158' }
    $ProfilePath = Join-Path $scriptRoot "..\runtime\compat\$folder\native-profile.json"
}
$profileData = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json
$GtaDir = [IO.Path]::GetFullPath($GtaDir)
$NativeAdapterPath = [IO.Path]::GetFullPath($NativeAdapterPath)
if (-not $AdapterManifestPath) { $AdapterManifestPath = "$NativeAdapterPath.json" }
$AdapterManifestPath = [IO.Path]::GetFullPath($AdapterManifestPath)
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
        fileVersion = $item.VersionInfo.FileVersion.Trim()
    }
}

function Test-GameFile([System.Collections.IDictionary]$Observed, [string]$ExpectedHash, [Nullable[int64]]$ExpectedSize, [string]$ExpectedVersion, [string]$Label) {
    if (-not $Observed.exists) {
        $issues.Add("Не найден $Label")
        return
    }
    if ($ExpectedVersion -and $Observed.fileVersion -ne $ExpectedVersion) {
        $issues.Add("Версия $Label: ожидалась $ExpectedVersion, получена $($Observed.fileVersion)")
    }
    if ($ExpectedSize.HasValue -and $Observed.size -ne $ExpectedSize.Value) {
        $issues.Add("Размер $Label не совпал")
    }
    if ($ExpectedHash -and $Observed.sha256 -ne $ExpectedHash.ToLowerInvariant()) {
        $issues.Add("SHA-256 $Label не совпал")
    }
}

$exe = Get-ObservedFile $profileData.gameExecutable
$update = Get-ObservedFile 'update\update.rpf'
$update2 = Get-ObservedFile 'update\update2.rpf'
$expectedSize = $null
if ($null -ne $profileData.gameSize -and "$($profileData.gameSize)" -ne '') {
    $expectedSize = [int64]$profileData.gameSize
}
Test-GameFile $exe ([string]$profileData.gameSha256) $expectedSize ([string]$profileData.gameFileVersion) $profileData.gameExecutable
if ($profileData.updateRpfSha256) { Test-GameFile $update ([string]$profileData.updateRpfSha256) $null '' 'update\update.rpf' }
if ($profileData.update2RpfSha256) { Test-GameFile $update2 ([string]$profileData.update2RpfSha256) $null '' 'update\update2.rpf' }
if (-not $profileData.gameSha256 -or -not $profileData.updateRpfSha256 -or -not $profileData.update2RpfSha256) {
    $issues.Add('профиль не содержит полного подтверждённого fingerprint')
}

$native = [ordered]@{ path = $NativeAdapterPath; exists = $false }
if (Test-Path -LiteralPath $NativeAdapterPath -PathType Leaf) {
    $native.exists = $true
    $native.size = (Get-Item -LiteralPath $NativeAdapterPath).Length
    $native.sha256 = (Get-FileHash -LiteralPath $NativeAdapterPath -Algorithm SHA256).Hash.ToLowerInvariant()
} else {
    $issues.Add("native adapter не найден: $NativeAdapterPath")
}

$adapterManifest = [ordered]@{ path = $AdapterManifestPath; exists = $false }
if (Test-Path -LiteralPath $AdapterManifestPath -PathType Leaf) {
    $adapterManifest.exists = $true
    try {
        $adapterManifest.data = Get-Content -LiteralPath $AdapterManifestPath -Raw | ConvertFrom-Json
        $expectedId = [string]$profileData.nativeClient.requiredAdapter
        if ([string]$adapterManifest.data.profile -ne $Profile) {
            $issues.Add("native adapter manifest profile не совпал: ожидался $Profile")
        }
        if ([string]$adapterManifest.data.id -ne $expectedId) {
            $issues.Add("native adapter manifest id не совпал: ожидался $expectedId")
        }
        if ([string]$adapterManifest.data.binary -ne [IO.Path]::GetFileName($NativeAdapterPath)) {
            $issues.Add('native adapter manifest binary не совпал с указанным файлом')
        }
        if ([string]::IsNullOrWhiteSpace([string]$adapterManifest.data.version)) {
            $issues.Add('native adapter manifest не содержит version')
        }
        if ([string]::IsNullOrWhiteSpace([string]$adapterManifest.data.sha256)) {
            $issues.Add('native adapter manifest не содержит sha256')
        } elseif ($native.exists -and $native.sha256 -ne ([string]$adapterManifest.data.sha256).ToLowerInvariant()) {
            $issues.Add('SHA-256 native adapter не совпал с его manifest')
        }
    } catch {
        $issues.Add("native adapter manifest повреждён: $($_.Exception.Message)")
    }
} else {
    $issues.Add("native adapter manifest не найден: $AdapterManifestPath")
}

$connector = [ordered]@{ path = $ConnectorPath; exists = $true }
if ($ConnectorPath) {
    $connector.exists = Test-Path -LiteralPath $ConnectorPath -PathType Leaf
    if (-not $connector.exists) { $issues.Add("connector не найден: $ConnectorPath") }
}

$e2e = $null
if ($E2EReport) {
    $e2e = Get-Content -LiteralPath $E2EReport -Raw | ConvertFrom-Json
    foreach ($name in @('gameWindowAlive', 'clientConnected', 'resourceLoaded', 'playerSpawned', 'twoClientSync')) {
        if (-not $e2e.e2eGate.$name) { $issues.Add("E2E-гейт не подтверждён: $name") }
    }
} else {
    $issues.Add('не передан отчёт двухклиентского E2E (-E2EReport)')
}

$report = [ordered]@{
    schema = 1
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    profile = $profile
    gtaDir = $GtaDir
    nativeAdapter = $native
    nativeAdapterManifest = $adapterManifest
    connector = $connector
    observed = [ordered]@{ exe = $exe; update = $update; update2 = $update2 }
    e2eGate = if ($e2e) { $e2e.e2eGate } else { $null }
    readyForRelease = ($issues.Count -eq 0)
    issues = @($issues)
    note = 'Наличие DLL само по себе не доказывает совместимость; readyForRelease требует успешного двухклиентского Windows E2E.'
}
$json = $report | ConvertTo-Json -Depth 10
if ($WriteReport) {
    $parent = Split-Path -Parent ([IO.Path]::GetFullPath($WriteReport))
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($WriteReport), $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}
$json
if ($issues.Count -gt 0) { exit 2 }
