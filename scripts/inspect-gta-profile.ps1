param(
    [Parameter(Mandatory = $true)]
    [string]$GtaDir,
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'
$GtaDir = [IO.Path]::GetFullPath($GtaDir)

function Get-FileInfo([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [ordered]@{ path = $Path; exists = $false }
    }
    $item = Get-Item -LiteralPath $Path
    return [ordered]@{
        path = $Path
        exists = $true
        size = $item.Length
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$exeName = @('GTA5.exe', 'GTA5_Enhanced.exe') |
    Where-Object { Test-Path -LiteralPath (Join-Path $GtaDir $_) -PathType Leaf } |
    Select-Object -First 1
if (-not $exeName) { throw "GTA V executable was not found in $GtaDir" }

$exe = Join-Path $GtaDir $exeName
$exeItem = Get-Item -LiteralPath $exe
$isEnhanced = $exeName -ieq 'GTA5_Enhanced.exe'
$platform = if (Test-Path (Join-Path $GtaDir '.egstore')) { 'epic' }
            elseif (Test-Path (Join-Path $GtaDir 'steam_api64.dll')) { 'steam' }
            else { 'rockstar-or-unknown' }

$report = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    gtaDir = $GtaDir
    edition = if ($isEnhanced) { 'enhanced' } else { 'legacy' }
    platform = $platform
    exe = [ordered]@{
        name = $exeName
        path = $exe
        fileVersion = $exeItem.VersionInfo.FileVersion
        productVersion = $exeItem.VersionInfo.ProductVersion
        size = $exeItem.Length
        sha256 = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    rpf = @(
        (Get-FileInfo (Join-Path $GtaDir 'update\update.rpf')),
        (Get-FileInfo (Join-Path $GtaDir 'update\update2.rpf'))
    )
    launchFiles = @(
        'PlayGTAV.exe', 'GTAVLauncher.exe', 'GTA5_BE.exe',
        'EOSSDK-Win64-Shipping.dll', 'steam_api64.dll', 'title.rgl'
    ) | ForEach-Object {
        [ordered]@{ name = $_; exists = Test-Path (Join-Path $GtaDir $_) }
    }
}

$json = $report | ConvertTo-Json -Depth 6
if ($OutFile) {
    $target = [IO.Path]::GetFullPath($OutFile)
    $parent = Split-Path -Parent $target
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [IO.File]::WriteAllText($target, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}
$json
