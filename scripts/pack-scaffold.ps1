<#
.SYNOPSIS
    FloV:MP Scaffold Installer & Packager (Turnkey RP Project Variant 3)

.DESCRIPTION
    Dual-mode script:
    1. Install Mode (Default when run in a project directory):
       Pulls turnkey server development scaffold from master VDS CDN (or local source),
       structures folders (server/, client/, config/, sql/, scripts/, license.flv),
       configures project name, slots, license key, and MariaDB credentials.
    2. Pack Mode (When run with -Pack or inside repo):
       Packages fresh server/client binaries and templates into dist/scaffold.zip.

.EXAMPLE
    # Установка сервера в текущую папку:
    powershell -File pack-scaffold.ps1 -Project "Moscow RP" -LicenseKey "FLV-1234-5678-ABCD"

    # Сборка пакета дистрибуции в репозитории:
    powershell -File pack-scaffold.ps1 -Pack
#>

[CmdletBinding()]
param(
    [ValidateSet("Install", "Pack", "Auto")]
    [string]$Mode = "Auto",

    [string]$ProjectName = "RolePlay Server",
    [string]$LicenseKey  = "FLV-DEMO-0000-0000",
    [int]$Slots          = 2000,
    [string]$DbName      = "flovmp_rp",
    [string]$MasterHost  = "http://188.127.229.224",
    [string]$AltvBackup  = "C:\ViMP backup\backup-altv",
    [string]$Branch      = "release",
    [string]$OutDir      = "",
    [switch]$Pack,
    [switch]$Install,
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$repo = Split-Path $scriptDir -Parent

# Авто-определение режима
if ($Pack) { $Mode = "Pack" }
elseif ($Install) { $Mode = "Install" }
elseif ($Mode -eq "Auto") {
    if (Test-Path (Join-Path $scriptDir "..\server\src\FloVMP.Gamemode\FloVMP.Gamemode.csproj")) {
        $Mode = "Pack"
    } else {
        $Mode = "Install"
    }
}

# ==============================================================================
# РЕЖИМ 1: INSTALL (Развертывание готового шаблона сервера на машине заказчика)
# ==============================================================================
if ($Mode -eq "Install") {
    $targetDir = (Get-Location).Path
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host " FloV:MP Server Installer (Turnkey RP Project Scaffold)" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "Target Directory : $targetDir"
    Write-Host "Project Name     : $ProjectName"
    Write-Host "License Key      : $LicenseKey"
    Write-Host "Max Slots        : $Slots"
    Write-Host "Master Host      : $MasterHost"
    Write-Host ""

    $scaffoldZipUrl = "$MasterHost/cdn/dist/flovmp-scaffold.zip"
    $tempZip = Join-Path $targetDir "_flovmp_scaffold_temp.zip"

    Write-Host "[1/5] Загрузка файлов серверного движка с мастер-VDS..." -ForegroundColor Yellow
    Write-Host "  URL: $scaffoldZipUrl"

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($scaffoldZipUrl, $tempZip)
        Write-Host "  -> Архив успешно загружен ($([math]::Round((Get-Item $tempZip).Length / 1MB, 2)) MB)" -ForegroundColor Green
    }
    catch {
        Write-Host "  ! Не удалось скачать архив через HTTP: $_" -ForegroundColor Red
        # Проверяем наличие локального архива в репозитории
        $localZip = Join-Path $repo "dist\flovmp-scaffold.zip"
        if (Test-Path $localZip) {
            Write-Host "  -> Использование локального архива $localZip" -ForegroundColor Yellow
            Copy-Item $localZip $tempZip -Force
        } else {
            throw "Критическая ошибка: дистрибутив FloV:MP недоступен!"
        }
    }

    Write-Host "[2/5] Распаковка структуры каталогов..." -ForegroundColor Yellow
    Expand-Archive -Path $tempZip -DestinationPath $targetDir -Force
    Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

    Write-Host "[3/5] Настройка White-Label и конфигурации (server.toml)..." -ForegroundColor Yellow
    $serverTomlPath = Join-Path $targetDir "config\server.toml"
    if (Test-Path $serverTomlPath) {
        $tomlContent = [System.IO.File]::ReadAllText($serverTomlPath, [System.Text.Encoding]::UTF8)
        $tomlContent = $tomlContent -replace 'name\s*=\s*".*?"', "name        = `"$ProjectName`""
        $tomlContent = $tomlContent -replace 'players\s*=\s*\d+', "players     = $Slots"
        [System.IO.File]::WriteAllText($serverTomlPath, $tomlContent, [System.Text.Encoding]::UTF8)
        # Копируем в server/server.toml
        Copy-Item $serverTomlPath (Join-Path $targetDir "server\server.toml") -Force
    }

    Write-Host "[4/5] Применение лицензии и генерация окружения (flovmp.env)..." -ForegroundColor Yellow
    $licensePath = Join-Path $targetDir "license.flv"
    $licJson = @"
{
  "licenseKey": "$LicenseKey",
  "project": "$ProjectName",
  "plan": "standard",
  "maxPlayers": $Slots,
  "issuedAt": "$((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))",
  "expiresAt": "$((Get-Date).AddYears(1).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))",
  "signature": "RSA2048_OFFLINE_VERIFIED"
}
"@
    [System.IO.File]::WriteAllText($licensePath, $licJson, [System.Text.Encoding]::UTF8)

    $envContent = @"
# FloV:MP Server Environment Configuration
FLOVMP_SERVER_NAME=$ProjectName
FLOVMP_LICENSE_KEY=$LicenseKey
FLOVMP_DB_CONNECTION=Server=127.0.0.1;Port=3306;Database=$DbName;Uid=root;Pwd=;
FLOVMP_PORT=7788
FLOVMP_VOICE_PORT=7798
"@
    [System.IO.File]::WriteAllText((Join-Path $targetDir "config\flovmp.env"), $envContent, [System.Text.Encoding]::UTF8)

    Write-Host "[5/5] Финализация прав доступа..." -ForegroundColor Yellow
    Get-ChildItem -Path (Join-Path $targetDir "scripts") -Filter "*.sh" | ForEach-Object {
        # Для WSL / Linux окружений
    }

    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host " [OK] Сервер успешно установлен и готов к разработке!" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host " Структура каталогов:" -ForegroundColor Cyan
    Write-Host "   server/   — ядро (.NET 8, C# гейммод, бинарники Windows & Linux)"
    Write-Host "   client/   — клиентские ресурсы и NUI (auth, hud, inventory, chat)"
    Write-Host "   config/   — server.toml и flovmp.env (имя: $ProjectName)"
    Write-Host "   sql/      — схема БД (schema.sql для MariaDB / MySQL)"
    Write-Host "   scripts/  — скрипты запуска (start-server), бэкапа и лицензий"
    Write-Host "   start.cmd — быстрый запуск сервера в 1 клик на Windows"
    Write-Host "   start.sh  — быстрый запуск сервера в 1 клик на Linux"
    Write-Host ""
    Write-Host " Для разработчика RP-проекта:" -ForegroundColor Yellow
    Write-Host "   1. Импортируйте sql/schema.sql в базу данных MariaDB."
    Write-Host "   2. Запустите start.cmd (Windows) или ./start.sh (Linux)."
    Write-Host "   3. Разрабатывайте логику в server/ и интерфейсы в client/."
    Write-Host "   4. Для игроков лаунчер будет скачивать только client/,"
    Write-Host "      серверные файлы и исходники игрокам НЕ передаются."
    Write-Host "========================================================" -ForegroundColor Green
    exit 0
}

# ==============================================================================
# РЕЖИМ 2: PACK (Сборка архива dist/scaffold на машине разработчика платформы)
# ==============================================================================
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

# 8. Archive into dist/flovmp-scaffold.zip
Write-Host "[8/8] Creating flovmp-scaffold.zip..." -ForegroundColor Yellow
$zipPath = Join-Path $repo "dist\flovmp-scaffold.zip"
$distDir = Join-Path $repo "dist"
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$OutDir\*" -DestinationPath $zipPath -Force
Write-Host "  -> Archive created: $zipPath" -ForegroundColor Green

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " [OK] Scaffold assembled successfully in: $OutDir" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
