[CmdletBinding()]
param(
    [string]$AltvBackup    = "C:\ViMP backup\backup-altv",
    [string]$Branch        = "release",
    [string]$OutDir        = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$repo = Split-Path $scriptDir -Parent
if (-not $OutDir) { $OutDir = Join-Path $repo "dist\linux-server" }

Write-Host "== Assembling Linux Server Package ==" -ForegroundColor Cyan
Write-Host "Repo:       $repo"
Write-Host "AltvBackup: $AltvBackup"
Write-Host "Output:     $OutDir"

if (-not (Test-Path $AltvBackup)) {
    throw "alt:V backup folder not found: $AltvBackup"
}

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
New-Item -ItemType Directory -Path "$OutDir\modules\js-module" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutDir\data" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutDir\resources\flovmp-core" -Force | Out-Null
New-Item -ItemType Directory -Path "$OutDir\resources\flovmp-client" -Force | Out-Null

Write-Host "[1/7] Copying Linux server binaries..." -ForegroundColor Yellow
Copy-Item "$AltvBackup\server\$Branch\x64_linux\altv-server" "$OutDir\altv-server" -Force
Copy-Item "$AltvBackup\server\$Branch\x64_linux\altv-crash-handler" "$OutDir\altv-crash-handler" -Force
Copy-Item "$AltvBackup\server\$Branch\x64_linux\update.json" "$OutDir\update.json" -Force

$dsn = "https://586f9304db234ff7bc949b843d4f92dd@sentry-alt.com/4"
$bytes = [System.IO.File]::ReadAllBytes("$OutDir\altv-server")
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
    [System.IO.File]::WriteAllBytes("$OutDir\altv-server", $bytes)
    Write-Host "  ~ Sentry DSN zeroed in altv-server at offset $idx"
} else {
    Write-Host "  ! Warning: Sentry DSN not found" -ForegroundColor Yellow
}

Write-Host "[2/7] Copying coreclr-module (C#)..." -ForegroundColor Yellow
Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_linux\AltV.Net.Host.dll" "$OutDir\AltV.Net.Host.dll" -Force
Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_linux\AltV.Net.Host.runtimeconfig.json" "$OutDir\AltV.Net.Host.runtimeconfig.json" -Force
Copy-Item "$AltvBackup\coreclr-module\$Branch\x64_linux\modules\libcsharp-module.so" "$OutDir\modules\libcsharp-module.so" -Force

Write-Host "[3/7] Copying js-module..." -ForegroundColor Yellow
Copy-Item "$AltvBackup\js-module\$Branch\x64_linux\modules\js-module\libjs-module.so" "$OutDir\modules\js-module\libjs-module.so" -Force
Copy-Item "$AltvBackup\js-module\$Branch\x64_linux\modules\js-module\libnode.so" "$OutDir\modules\js-module\libnode.so" -Force

Write-Host "[4/7] Copying data .bin files..." -ForegroundColor Yellow
Get-ChildItem "$AltvBackup\data\$Branch\data\*.bin" | ForEach-Object {
    Copy-Item $_.FullName "$OutDir\data\$($_.Name)" -Force
}

Write-Host "[5/7] Publishing FloVMP.Gamemode (Release)..." -ForegroundColor Yellow
$coreProj = Join-Path $repo "server\src\FloVMP.Gamemode\FloVMP.Gamemode.csproj"
& dotnet publish $coreProj -c $Configuration -o "$OutDir\resources\flovmp-core" --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
Copy-Item "$repo\server\resources\flovmp-core\resource.toml" "$OutDir\resources\flovmp-core\resource.toml" -Force
Remove-Item "$OutDir\resources\flovmp-core\*.pdb" -Force -ErrorAction SilentlyContinue

Write-Host "[6/7] Copying client JS resource..." -ForegroundColor Yellow
Copy-Item "$repo\client\resources\flovmp-client\*" "$OutDir\resources\flovmp-client\" -Recurse -Force

Write-Host "[7/7] Generating configuration and launcher scripts..." -ForegroundColor Yellow
Copy-Item "$repo\config\server.toml" "$OutDir\server.toml" -Force

$startLines = @(
    "#!/bin/bash",
    "cd ""`$(dirname ""`$0"")""",
    "chmod +x altv-server altv-crash-handler",
    "export LD_LIBRARY_PATH=""modules:modules/js-module:`$LD_LIBRARY_PATH""",
    "./altv-server"
)
[System.IO.File]::WriteAllLines("$OutDir\start.sh", $startLines)

$serviceLines = @(
    "[Unit]",
    "Description=FloV:MP Server (Derzhava Online)",
    "After=network.target",
    "",
    "[Service]",
    "Type=simple",
    "User=root",
    "WorkingDirectory=/opt/flovmp",
    "ExecStart=/opt/flovmp/start.sh",
    "Restart=on-failure",
    "RestartSec=5",
    "LimitNOFILE=65535",
    "",
    "[Install]",
    "WantedBy=multi-user.target"
)
[System.IO.File]::WriteAllLines("$OutDir\flovmp.service", $serviceLines)

Write-Host "== Linux Server Package Successfully Assembled in $OutDir ==" -ForegroundColor Green
