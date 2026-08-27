<#
.SYNOPSIS
    Generates a FloV:MP CDN manifest.json from a folder's contents.

.DESCRIPTION
    Walks -SourceDir recursively, records every file's relative path (with '/'),
    size and lowercase SHA-256. Output is the manifest format consumed by the
    launcher (FloVMP.Launcher.Services.Cdn.Manifest).

    This is the CDN-side packaging tool. For a real deployment you'd upload the
    same folder tree to the CDN and set -BaseUrl to its public root.

    ASCII-only (Windows PowerShell 5.1 codepage parsing).

.PARAMETER SourceDir
    Folder whose contents become the manifest (e.g. runtime\client).

.PARAMETER OutFile
    Where to write manifest.json.

.PARAMETER Version
    Free-form version string stored in the manifest.

.PARAMETER Branch
    alt:V client branch this set targets. Default release.

.PARAMETER BaseUrl
    Public root the files will be served from. May be an http(s) URL or, for
    offline testing, a local folder path. Empty = launcher uses the manifest's
    own location as the base.

.EXAMPLE
    powershell -File scripts/make-manifest.ps1 -SourceDir runtime\client -OutFile runtime\client\manifest.json -Version dev-1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$SourceDir,
    [Parameter(Mandatory)] [string]$OutFile,
    [string]$Version = "",
    [ValidateSet("release", "rc", "dev")]
    [string]$Branch  = "release",
    [string]$BaseUrl = ""
)

$ErrorActionPreference = "Stop"
$SourceDir = (Resolve-Path $SourceDir).Path
if (-not (Test-Path $SourceDir)) { throw "SourceDir not found: $SourceDir" }
if (-not $Version) { $Version = "manual-" + (Get-Date -Format "yyyyMMdd-HHmmss") }

$outFull = [System.IO.Path]::GetFullPath($OutFile)

$files = @()
Get-ChildItem $SourceDir -Recurse -File | ForEach-Object {
    if ($_.FullName -eq $outFull) { return }  # don't include the manifest itself
    $rel = $_.FullName.Substring($SourceDir.Length + 1) -replace '\\', '/'
    $sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLower()
    $files += [ordered]@{
        path   = $rel
        sha256 = $sha
        size   = $_.Length
        url    = ""
    }
}

$manifest = [ordered]@{
    version = $Version
    branch  = $Branch
    baseUrl = $BaseUrl
    files   = $files
}

$dir = Split-Path $outFull -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outFull -Encoding UTF8

Write-Host ("manifest: {0} files, version {1}" -f $files.Count, $Version) -ForegroundColor Green
Write-Host ("written : {0}" -f $outFull)
