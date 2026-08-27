<#
.SYNOPSIS
    Assembles a runnable FloV:MP server runtime into C:\FloV-MP\runtime\server.

.DESCRIPTION
    Runtime = alt:V engine binaries (external dependency, NOT stored in git)
    + published C# gamemode + client JS resource + server.toml.

    Engine source: a backup of the official alt:V binaries
    (default C:\ViMP backup\backup-altv, branch "release").

    Idempotent: wipes runtime\server\{modules,resources,data} and rebuilds.
    Leaves .server-crashes-cache / cache folders untouched.

    NOTE: keep this file ASCII-only. Windows PowerShell 5.1 parses .ps1 in the
    system ANSI codepage; non-ASCII here breaks the parser. Russian docs live
    in docs/*.md instead.

.PARAMETER AltvBackup
    Root of the alt:V backup.

.PARAMETER Branch
    Binary branch: release | rc | dev. Default: release.

.PARAMETER OutRoot
    Assemble target. Default: <repo>\runtime.

.PARAMETER Configuration
    C# build configuration. Default: Release.

.EXAMPLE
    powershell -File scripts/assemble-runtime.ps1
    powershell -File scripts/assemble-runtime.ps1 -Branch rc
#>
[CmdletBinding()]
param(
    [string]$AltvBackup    = "C:\ViMP backup\backup-altv",
    [ValidateSet("release", "rc", "dev")]
    [string]$Branch        = "release",
    [string]$OutRoot       = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Robust script-dir detection (5.1 can leave $PSScriptRoot empty in param defaults).
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$repo   = Split-Path $scriptDir -Parent
if (-not $OutRoot) { $OutRoot = Join-Path $repo "runtime" }
$server = Join-Path $OutRoot "server"
$plat   = "x64_win32"

function Copy-Required {
    param([string]$From, [string]$To)
    if (-not (Test-Path $From)) {
        throw "Missing required source file: $From"
    }
    $dir = Split-Path $To -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -LiteralPath $From -Destination $To -Force
    Write-Host "  + $($To.Substring($server.Length + 1))"
}

Write-Host "== FloV:MP assemble-runtime ==" -ForegroundColor Cyan
Write-Host "repo      : $repo"
Write-Host "altv src  : $AltvBackup (branch $Branch)"
Write-Host "runtime   : $server"
Write-Host ""

if (-not (Test-Path $AltvBackup)) {
    throw "alt:V backup folder not found: $AltvBackup"
}

# --- 1. wipe only what we generate (engine / data / resources) --------
foreach ($sub in @("modules", "resources", "data")) {
    $p = Join-Path $server $sub
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
New-Item -ItemType Directory -Path $server -Force | Out-Null

# --- 2. server binaries ---------------------------------------------
Write-Host "[server binaries]" -ForegroundColor Yellow
$srvSrc = Join-Path $AltvBackup "server\$Branch\$plat"
Copy-Required "$srvSrc\altv-server.exe"         "$server\altv-server.exe"
Copy-Required "$srvSrc\altv-crash-handler.exe"  "$server\altv-crash-handler.exe"
Copy-Required "$srvSrc\update.json"             "$server\update.json"

# --- 3. coreclr-module (C#) ---------------------------------------
Write-Host "[coreclr-module]" -ForegroundColor Yellow
$clrSrc = Join-Path $AltvBackup "coreclr-module\$Branch\$plat"
Copy-Required "$clrSrc\AltV.Net.Host.dll"                "$server\AltV.Net.Host.dll"
Copy-Required "$clrSrc\AltV.Net.Host.runtimeconfig.json" "$server\AltV.Net.Host.runtimeconfig.json"
Copy-Required "$clrSrc\modules\csharp-module.dll"        "$server\modules\csharp-module.dll"

# --- 4. js-module (client scripts) ------------------------------
Write-Host "[js-module]" -ForegroundColor Yellow
$jsSrc = Join-Path $AltvBackup "js-module\$Branch\$plat\modules\js-module"
Copy-Required "$jsSrc\js-module.dll" "$server\modules\js-module\js-module.dll"
Copy-Required "$jsSrc\libnode.dll"   "$server\modules\js-module\libnode.dll"

# --- 5. data/*.bin (vehicle/ped/weapon/clothes models) ---------
Write-Host "[data]" -ForegroundColor Yellow
$dataSrc = Join-Path $AltvBackup "data\$Branch\data"
Get-ChildItem "$dataSrc\*.bin" | ForEach-Object {
    Copy-Required $_.FullName "$server\data\$($_.Name)"
}

# --- 6. C# gamemode: dotnet publish -> resources\flovmp-core ---
Write-Host "[flovmp-core: dotnet publish]" -ForegroundColor Yellow
$coreProj = Join-Path $repo "server\src\FloVMP.Gamemode\FloVMP.Gamemode.csproj"
$coreOut  = Join-Path $server "resources\flovmp-core"
& dotnet publish $coreProj -c $Configuration -o $coreOut --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
Copy-Item (Join-Path $repo "server\resources\flovmp-core\resource.toml") (Join-Path $coreOut "resource.toml") -Force
Remove-Item (Join-Path $coreOut "*.pdb") -Force -ErrorAction SilentlyContinue
Write-Host "  -> resources\flovmp-core"

# --- 7. client JS resource ------------------------------------
Write-Host "[flovmp-client: copy]" -ForegroundColor Yellow
$cliSrc = Join-Path $repo "client\resources\flovmp-client"
$cliOut = Join-Path $server "resources\flovmp-client"
Copy-Item $cliSrc $cliOut -Recurse -Force
Write-Host "  -> resources\flovmp-client"

# --- 8. server.toml -----------------------------------------
Write-Host "[server.toml]" -ForegroundColor Yellow
Copy-Item (Join-Path $repo "config\server.toml") (Join-Path $server "server.toml") -Force
Write-Host "  -> server.toml"

Write-Host ""
Write-Host "== Done. Run: powershell -File scripts/run-server.ps1 ==" -ForegroundColor Green
