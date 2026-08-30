[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)] [string]$SourceGta,
    [Parameter(Mandatory=$true)] [string]$SteamContent,
    [Parameter(Mandatory=$true)] [string]$UpdateContent,
    [Parameter(Mandatory=$true)] [string]$OutputGta
)

$ErrorActionPreference = 'Stop'

# Do not mix Epic/Rockstar bootstrap files into a Steam staging profile.
# These files can prevent GTA from creating a process window at all.
$platformSpecificSourceFiles = @(
    'GTA5_BE.exe',
    'EOSSDK-Win64-Shipping.dll',
    'EOSSDK-Win64-Shipping-1.17.1.3.dll',
    'Rockstar-Games-Epic.exe',
    'Rockstar-Games-Launcher.exe',
    'versioninfo.txt'
)

foreach ($path in @($SourceGta, $SteamContent, $UpdateContent)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Directory not found: $path"
    }
}
if (Test-Path -LiteralPath $OutputGta) {
    throw "Output already exists; refusing to overwrite: $OutputGta"
}

New-Item -ItemType Directory -Path $OutputGta | Out-Null

# Hard-link the user's installed Legacy files to avoid copying ~100 GB.
# The source installation remains untouched: links are never modified in place.
$steamFiles = @{}
Get-ChildItem -LiteralPath $SteamContent -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\.DepotDownloader\\' } |
    ForEach-Object {
        $relative = $_.FullName.Substring($SteamContent.Length).TrimStart('\')
        $steamFiles[$relative.ToLowerInvariant()] = $_.FullName
    }

Get-ChildItem -LiteralPath $SourceGta -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\update\\' -and
        $_.Name -ne 'GTA5.exe' -and
        -not $platformSpecificSourceFiles.Contains($_.Name) -and
        -not $steamFiles.ContainsKey($_.FullName.Substring($SourceGta.Length).TrimStart('\').ToLowerInvariant())
    } |
    ForEach-Object {
        $relative = $_.FullName.Substring($SourceGta.Length).TrimStart('\')
        $target = Join-Path $OutputGta $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        New-Item -ItemType HardLink -Path $target -Target $_.FullName | Out-Null
    }

# Overlay the matching Steam launcher/runtime files and GTA5.exe.
foreach ($entry in $steamFiles.GetEnumerator()) {
    $target = Join-Path $OutputGta $entry.Key
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $entry.Value -Destination $target -Force
}

# Overlay only the two RPF files required by the alt:V 16.4.39 compatibility ceiling.
$required = @('update\update.rpf', 'update\update2.rpf')
foreach ($relative in $required) {
    $source = Join-Path $UpdateContent $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Required update file not found: $source"
    }
    $target = Join-Path $OutputGta $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

$exe = Join-Path $OutputGta 'GTA5.exe'
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
Write-Host "Staging ready: $OutputGta" -ForegroundColor Green
Write-Host "GTA5.exe version: $version"
Write-Host "Source installation was not modified."
