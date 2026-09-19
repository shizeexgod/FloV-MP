# Дымовой тест Windows-пакета: то, что проверялось руками перед каждой сдачей.
#
#   распаковка архива в пустую папку (путь с русскими буквами)
#   -> первый запуск FloVMP-Server.exe (создание настроек, папки gamemode)
#   -> сборка своего сервера из шаблона (gamemode\build.cmd)
#   -> запуск на свободных портах с командами консоли
#   -> проверка лога: платформа поднялась, свой ресурс загружен, команды ответили
#
# Запуск:  powershell -ExecutionPolicy Bypass -File scripts\smoke_windows.ps1
#          [-Zip dist\server\flovmp-server-1.0.0-windows.zip] [-Port 7790]
# Код возврата: 0 — всё прошло, 1 — есть провалы.

param(
    [string]$Zip = "",
    [int]$Port = 7790
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Zip) {
    $Zip = Get-ChildItem (Join-Path $repo 'dist\server') -Filter 'flovmp-server-*-windows.zip' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Zip -or -not (Test-Path $Zip)) { Write-Host "Нет архива пакета — соберите: python scripts\pack_server.py"; exit 1 }

$work = Join-Path $env:TEMP ("флов-смоук-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $work | Out-Null
$fails = 0
function Check($ok, $name) {
    if ($ok) { Write-Host "  [PASS] $name" -ForegroundColor Green }
    else { Write-Host "  [FAIL] $name" -ForegroundColor Red; $script:fails++ }
}

function Run-Server($root, $stdinText, $seconds, $tag) {
    $in = Join-Path $work "stdin-$tag.txt"
    Set-Content $in $stdinText -Encoding ASCII
    $p = Start-Process -FilePath (Join-Path $root 'FloVMP-Server.exe') -WorkingDirectory $root -PassThru `
        -RedirectStandardInput $in -RedirectStandardOutput (Join-Path $work "out-$tag.txt") `
        -RedirectStandardError (Join-Path $work "err-$tag.txt")
    Start-Sleep $seconds
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    Start-Sleep 3
    return (Get-Content (Join-Path $work "out-$tag.txt") -Raw -Encoding UTF8)
}

try {
    Write-Host "=== Распаковка: $Zip"
    Expand-Archive $Zip -DestinationPath $work -Force
    $packageRoot = Get-ChildItem $work -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'manifest.txt') } |
        Select-Object -First 1 -ExpandProperty FullName
    Check ([bool]$packageRoot) 'в архиве есть manifest.txt'
    if (-not $packageRoot) { exit 1 }

    Write-Host "=== Установка через install.cmd (проверка SHA-256 и staging)"
    $root = Join-Path $work 'installed'
    $installOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $packageRoot 'install.ps1') `
        -InstallDir $root -NoStart 2>&1 | Out-String
    Check ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $root 'FloVMP-Server.exe'))) 'install.ps1 установил проверенный пакет'
    if (-not (Test-Path (Join-Path $root 'FloVMP-Server.exe'))) { Write-Host $installOutput; exit 1 }

    Write-Host "=== Первый запуск (создание настроек)"
    Run-Server $root '' 8 'init' | Out-Null
    Check (Test-Path (Join-Path $root 'config\flovmp.env')) 'создан config\flovmp.env'
    Check (Test-Path (Join-Path $root 'server\server.toml')) 'создан server\server.toml'
    Check (Test-Path (Join-Path $root 'voice\voice.toml')) 'создан voice\voice.toml'
    Check (Test-Path (Join-Path $root 'gamemode\build.cmd')) 'создана папка gamemode'
    $toml = Get-Content (Join-Path $root 'server\server.toml') -Raw -Encoding UTF8
    Check ($toml -notmatch '__FLOVMP_') 'в server.toml подставлены все значения'

    # Свободные порты: тест не должен мешать запущенному рядом серверу.
    $voicePub = $Port + 5; $voiceInt = $Port + 6
    $utf8 = New-Object Text.UTF8Encoding($false)
    $t = [IO.File]::ReadAllText((Join-Path $root 'server\server.toml'))
    $t = $t -replace '(?m)^port\s*=\s*\d+', "port        = $Port"
    $t = $t -replace '(?m)^externalPort\s*=\s*\d+', "externalPort       = $voiceInt"
    $t = $t -replace '(?m)^externalPublicPort\s*=\s*\d+', "externalPublicPort = $voicePub"
    [IO.File]::WriteAllText((Join-Path $root 'server\server.toml'), $t, $utf8)
    $v = [IO.File]::ReadAllText((Join-Path $root 'voice\voice.toml'))
    $v = $v -replace '(?m)^port\s*=\s*\d+', "port = $voiceInt" -replace '(?m)^playerPort\s*=\s*\d+', "playerPort = $voicePub"
    [IO.File]::WriteAllText((Join-Path $root 'voice\voice.toml'), $v, $utf8)

    Write-Host "=== Сборка своего сервера (gamemode\build.cmd)"
    # Как при двойном клике: build.cmd по полному пути. Запуск с рабочей папкой
    # из русских букв через Start-Process cmd.exe не находит сам файл.
    $buildCmd = Join-Path $root 'gamemode' | Join-Path -ChildPath 'build.cmd'
    $build = & cmd.exe /c "`"$buildCmd`" < NUL" 2>&1 | Out-String
    Check ($build -match 'Сборка успешно завершена|Build succeeded') 'gamemode собирается из шаблона'
    Check (Test-Path (Join-Path $root 'server\resources\gamemode\resource.toml')) 'ресурс gamemode на месте'

    Write-Host "=== Запуск с командами консоли"
    Run-Server $root "license`nonline`nbans`nreloadadmins" 35 'run' | Out-Null
    $log = Get-Content (Join-Path $root 'server\server.log') -Raw -Encoding UTF8
    Check ($log -match 'Платформа запущена') 'платформа поднялась'
    Check ($log -match 'Loaded resource gamemode') 'свой ресурс gamemode загружен'
    Check ($log -match 'зарегистрирована команда /hello') 'свой ресурс зарегистрировал команду'
    Check ($log -match 'Проверка движения включена') 'проверка движения включена'
    Check ($log -match 'admin-commands\.cfg') 'файл уровней команд создан/прочитан'
    Check ($log -match '\[Console\] Лимит игроков') 'консоль: license ответила'
    Check ($log -match 'ДЕЙСТВУЮЩИЕ БЛОКИРОВКИ') 'консоль: bans ответила'
    Check ($log -match 'Онлайн: 0') 'консоль: online ответила'
    Check ($log -notmatch 'Unhandled exception|System\.\w+Exception') 'в логе нет необработанных исключений'
    Check (Test-Path (Join-Path $root 'server\config\admin-commands.cfg')) 'server\config\admin-commands.cfg на месте'

    Write-Host "=== Целостность файлов платформы"
    $check = powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\check-files.ps1') 2>&1 | Out-String
    Check ($check -match 'Все файлы платформы на месте') 'scripts\check-files.ps1: файлы целы'
}
finally {
    Get-CimInstance Win32_Process -Filter "name='flovmp-server.exe' or name='altv-voice-server.exe' or name='FloVMP-Server.exe'" |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($work) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep 2
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
if ($fails -eq 0) { Write-Host "Дымовой тест пройден." -ForegroundColor Green; exit 0 }
Write-Host "Провалов: $fails" -ForegroundColor Red
exit 1
