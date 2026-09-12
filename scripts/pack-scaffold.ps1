<#
.SYNOPSIS
    FloV:MP Scaffold Packager (Turnkey RP Project Variant 3)
.DESCRIPTION
    Packages clean turnkey project structure:
    server/
    client/
    config/
    sql/
    scripts/
    license.flv
    start.cmd / start.sh
    README.md
#>

[CmdletBinding()]
param(
    [string]$AltvBackup = "C:\ViMP backup\backup-altv",
    [string]$Branch     = "release",
    [string]$OutDir     = "",
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$repo = Split-Path $scriptDir -Parent
if (-not $OutDir) { $OutDir = Join-Path $repo "dist\scaffold" }

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " FloV:MP Scaffold Packager (Turnkey RP Project Variant 3)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Repo:       $repo"
Write-Host "AltvBackup: $AltvBackup"
Write-Host "Output:     $OutDir"
Write-Host ""

if (-not (Test-Path $AltvBackup)) {
    throw "alt:V backup folder not found: $AltvBackup"
}

# 1. Clean & prepare directory tree
if (Test-Path $OutDir) {
    Write-Host "[1/8] Cleaning previous scaffold build..." -ForegroundColor Yellow
    Remove-Item $OutDir -Recurse -Force
}

$dirs = @(
    "$OutDir\server\modules\js-module",
    "$OutDir\server\data",
    "$OutDir\server\resources\flovmp-core",
    "$OutDir\server\resources\flovmp-client",
    "$OutDir\client\resources\flovmp-client",
    "$OutDir\config",
    "$OutDir\sql",
    "$OutDir\scripts",
    "$OutDir\backups"
)
foreach ($d in $dirs) {
    New-Item -ItemType Directory -Path $d -Force | Out-Null
}

# 2. Build & publish C# gamemode
Write-Host "[2/8] Publishing C# gamemode (FloVMP.Gamemode)..." -ForegroundColor Yellow
$gamemodeProj = Join-Path $repo "server\src\FloVMP.Gamemode\FloVMP.Gamemode.csproj"
& dotnet publish $gamemodeProj -c Release -o "$OutDir\server\resources\flovmp-core" --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
Copy-Item "$repo\server\resources\flovmp-core\resource.toml" "$OutDir\server\resources\flovmp-core\resource.toml" -Force
Remove-Item "$OutDir\server\resources\flovmp-core\*.pdb" -Force -ErrorAction SilentlyContinue

# 3. Copy engine binaries & neutralize Sentry
Write-Host "[3/8] Copying engine binaries..." -ForegroundColor Yellow

# Windows binaries
Copy-Item "$AltvBackup\server\$Branch\x64_win32\altv-server.exe" "$OutDir\server\altv-server.exe" -Force
Copy-Item "$AltvBackup\server\$Branch\x64_win32\altv-crash-handler.exe" "$OutDir\server\altv-crash-handler.exe" -Force
Copy-Item "$AltvBackup\server\$Branch\x64_win32\update.json" "$OutDir\server\update.json" -Force

# Windows modules
Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_win32\AltV.Net.Host.dll" "$OutDir\server\AltV.Net.Host.dll" -Force
Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_win32\AltV.Net.Host.runtimeconfig.json" "$OutDir\server\AltV.Net.Host.runtimeconfig.json" -Force
Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_win32\modules\csharp-module.dll" "$OutDir\server\modules\csharp-module.dll" -Force
Copy-Item "$AltvBackup\js-module\$Branch\x64_win32\modules\js-module\js-module.dll" "$OutDir\server\modules\js-module\js-module.dll" -Force
Copy-Item "$AltvBackup\js-module\$Branch\x64_win32\modules\js-module\libnode.dll" "$OutDir\server\modules\js-module\libnode.dll" -Force

# Linux binaries
if (Test-Path "$AltvBackup\server\$Branch\x64_linux\altv-server") {
    Copy-Item "$AltvBackup\server\$Branch\x64_linux\altv-server" "$OutDir\server\altv-server" -Force
    Copy-Item "$AltvBackup\server\$Branch\x64_linux\altv-crash-handler" "$OutDir\server\altv-crash-handler" -Force
    Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_linux\modules\libcsharp-module.so" "$OutDir\server\modules\libcsharp-module.so" -Force
    Copy-Item "$AltvBackup\js-module\$Branch\x64_linux\modules\js-module\libjs-module.so" "$OutDir\server\modules\js-module\libjs-module.so" -Force
    Copy-Item "$AltvBackup\js-module\$Branch\x64_linux\modules\js-module\libnode.so" "$OutDir\server\modules\js-module\libnode.so" -Force
}

function Neutralize-Sentry([string]$FilePath) {
    if (-not (Test-Path $FilePath)) { return }
    $dsn = "https://586f9304db234ff7bc949b843d4f92dd@sentry-alt.com/4"
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $nb = [System.Text.Encoding]::ASCII.GetBytes($dsn)
    $idx = -1
    for ($i = 0; $i -le $bytes.Length - $nb.Length; $i++) {
        $hit = $true
        for ($j = 0; $j -lt $nb.Length; $j++) {
            if ($bytes[$i + $j] -ne $nb[$j]) { $hit = $false; break }
        }
        if ($hit) { $idx = $i; break }
    }
    if ($idx -ge 0) {
        for ($j = 0; $j -lt $nb.Length; $j++) { $bytes[$idx + $j] = 0 }
        [System.IO.File]::WriteAllBytes($FilePath, $bytes)
        Write-Host "  ~ Sentry DSN zeroed in $(Split-Path $FilePath -Leaf)"
    }
}
Neutralize-Sentry "$OutDir\server\altv-server.exe"
Neutralize-Sentry "$OutDir\server\altv-server"

# 4. Copy data caches (.bin)
Write-Host "[4/8] Copying entity caches (.bin)..." -ForegroundColor Yellow
Get-ChildItem "$AltvBackup\data\$Branch\data\*.bin" | ForEach-Object {
    Copy-Item $_.FullName "$OutDir\server\data\$($_.Name)" -Force
}

# 5. Copy client resources (NUI & scripts)
Write-Host "[5/8] Copying client resources..." -ForegroundColor Yellow
Copy-Item "$repo\client\resources\flovmp-client\*" "$OutDir\client\resources\flovmp-client\" -Recurse -Force
Copy-Item "$repo\client\resources\flovmp-client\*" "$OutDir\server\resources\flovmp-client\" -Recurse -Force

# 6. Copy configs and SQL schema
Write-Host "[6/8] Copying config & SQL schema..." -ForegroundColor Yellow
Copy-Item "$repo\config\server.toml" "$OutDir\config\server.toml" -Force
Copy-Item "$repo\config\server.toml" "$OutDir\server\server.toml" -Force
Copy-Item "$repo\sql\schema.sql" "$OutDir\sql\schema.sql" -Force

# 7. Deploy templates
Write-Host "[7/8] Deploying template scripts and shortcuts..." -ForegroundColor Yellow
$tplDir = Join-Path $scriptDir "scaffold-templates"
Copy-Item "$tplDir\start-server.cmd"   "$OutDir\scripts\start-server.cmd" -Force
Copy-Item "$tplDir\start-server.sh"    "$OutDir\scripts\start-server.sh" -Force
Copy-Item "$tplDir\backup-db.cmd"      "$OutDir\scripts\backup-db.cmd" -Force
Copy-Item "$tplDir\backup-db.sh"       "$OutDir\scripts\backup-db.sh" -Force
Copy-Item "$tplDir\update-license.cmd" "$OutDir\scripts\update-license.cmd" -Force
Copy-Item "$tplDir\update-license.sh"  "$OutDir\scripts\update-license.sh" -Force
Copy-Item "$tplDir\flovmp.env.example" "$OutDir\config\flovmp.env.example" -Force
Copy-Item "$tplDir\start.cmd"          "$OutDir\start.cmd" -Force
Copy-Item "$tplDir\start.sh"           "$OutDir\start.sh" -Force
Copy-Item "$tplDir\license.flv"        "$OutDir\license.flv" -Force
Copy-Item "$tplDir\README.md"          "$OutDir\README.md" -Force

# 8. Archive if requested
if ($CreateZip) {
    Write-Host "[8/8] Creating flovmp-scaffold.zip..." -ForegroundColor Yellow
    $zipPath = Join-Path $repo "dist\flovmp-scaffold.zip"
    $distDir = Join-Path $repo "dist"
    if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$OutDir\*" -DestinationPath $zipPath -Force
    Write-Host "  -> Archive created: $zipPath" -ForegroundColor Green
} else {
    Write-Host "[8/8] Skipping zip archiving (-CreateZip not specified)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " [OK] Scaffold assembled successfully in: $OutDir" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
