param(
    [string]$GtaDir = "",
    [switch]$Yes,
    [switch]$Uninstall,
    [switch]$KeepBattlEye,
    [switch]$NoShortcut,
    [switch]$Elevated
)
# Установка клиента FloV:MP в GTA V Legacy 1.0.3889.0.
#
#   install-client.cmd                  — установить (всё само)
#   install-client.cmd -GtaDir "D:\GTAV" — если игра не нашлась
#   install-client.cmd -Uninstall       — удалить (вернуть игру как было)
#
# Что делает:
#   1) находит игру (Epic Games, Steam — все библиотеки, Rockstar Games Launcher);
#   2) проверяет, что это Legacy 1.0.3889.0 (Enhanced не подходит);
#   3) ставит ScriptHookV с официального сайта dev-c.com, сверяя SHA-256
#      (распространять ScriptHookV в своём архиве автор запрещает);
#   4) включает штатный режим Rockstar «без BattlEye» (-nobattleye в args.txt
#      папки игры): моды работают только в сюжетном режиме, GTA Online в этом
#      режиме недоступна — это ограничение Rockstar;
#   5) кладёт FloVMP.asi и создаёт ярлык «FloV:MP» на рабочем столе.
# Файлы самой игры (GTA5.exe, *.rpf) не изменяются. Всё, что добавлено,
# записывается в %LOCALAPPDATA%\FloVMP\install.json — -Uninstall убирает ровно это.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$GameVersion = '1.0.3889.0'
$EpicLegacyApp = '9d2d0eb64d5c44529cece33fe2a46482'
$ShvUrl = 'http://www.dev-c.com/files/ScriptHookV_3889.0_1158.13.zip'
$ShvPage = 'http://www.dev-c.com/gtav/scripthookv/'
$ShvSha256 = 'B64C97C3353906F14621E7E9511E4AEC2A7D436ECC21ED124D3816585E2E6188'
$DataDir = Join-Path $env:LOCALAPPDATA 'FloVMP'
$StateFile = Join-Path $DataDir 'install.json'
New-Item -ItemType Directory -Force (Join-Path $DataDir 'logs') | Out-Null
$LogFile = Join-Path $DataDir 'logs\install.log'

function Say($t, $c = 'Gray') {
    Write-Host "[FloV:MP] $t" -ForegroundColor $c
    try { Add-Content -Path $LogFile -Value ("{0:HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8 } catch { }
}
# Окно, открытое с правами администратора, закрылось бы сразу — с итогом.
function Finish($code) { if ($Elevated) { Read-Host 'Нажмите Enter, чтобы закрыть окно' | Out-Null }; exit $code }
function Fail($t, $code) { Say $t Red; Finish $code }
Add-Content -Path $LogFile -Value ("`n==== {0:yyyy-MM-dd HH:mm:ss} install-client {1}" -f (Get-Date), (($PSBoundParameters.Keys | ForEach-Object { "-$_" }) -join ' ')) -Encoding UTF8

function Get-State {
    if (Test-Path $StateFile) { try { return Get-Content $StateFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { } }
    return $null
}
function Save-State($s) { $s | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8 }

function Test-Writable($dir) {
    try {
        $probe = Join-Path $dir ('.flovmp-write-' + [guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($probe, 'x'); Remove-Item $probe -Force; return $true
    } catch { return $false }
}

function Restart-Elevated {
    # Папка игры в Program Files: без прав администратора туда не записать.
    Say 'Нужны права администратора (папка игры защищена Windows) — перезапускаю установщик...' Yellow
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($GtaDir) { $argList += @('-GtaDir', "`"$GtaDir`"") }
    if ($Yes) { $argList += '-Yes' }
    if ($Uninstall) { $argList += '-Uninstall' }
    if ($KeepBattlEye) { $argList += '-KeepBattlEye' }
    if ($NoShortcut) { $argList += '-NoShortcut' }
    $argList += '-Elevated'
    try {
        $p = Start-Process powershell.exe -ArgumentList $argList -Verb RunAs -Wait -PassThru
        exit $p.ExitCode
    } catch {
        Fail 'Без прав администратора установить нельзя: запустите install-client.cmd правой кнопкой → «Запуск от имени администратора».' 7
    }
}

# --- поиск игры ------------------------------------------------------------------
function Get-GameCandidates {
    $list = New-Object System.Collections.Generic.List[object]
    $add = { param($path, $source) if ($path -and (Test-Path (Join-Path $path 'GTA5.exe'))) { $list.Add([pscustomobject]@{ Path = (Resolve-Path $path).Path.TrimEnd('\'); Source = $source }) } }
    # Epic Games — манифесты установленных игр (отличают Legacy от Enhanced).
    Get-ChildItem 'C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests' -Filter *.item -ErrorAction SilentlyContinue | ForEach-Object {
        try { $j = Get-Content $_.FullName -Raw | ConvertFrom-Json; if ($j.AppName -eq $EpicLegacyApp) { & $add $j.InstallLocation 'Epic Games' } } catch { }
    }
    # Rockstar / Steam / Epic — реестр игры.
    foreach ($k in 'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V', 'HKLM:\SOFTWARE\Rockstar Games\Grand Theft Auto V') {
        $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
        if ($p) { & $add $p.InstallFolderEpic 'Epic Games'; & $add $p.InstallFolderSteam 'Steam'; & $add $p.InstallFolder 'Rockstar Games' }
    }
    # Steam — все библиотеки из libraryfolders.vdf.
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steam) {
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        $libs = @($steam)
        if (Test-Path $vdf) { $libs += (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value -replace '\\\\', '\' }) }
        foreach ($l in $libs) { & $add (Join-Path $l 'steamapps\common\Grand Theft Auto V') 'Steam' }
    }
    & $add "$env:ProgramFiles\Epic Games\GTAV" 'Epic Games'
    & $add "$env:ProgramFiles\Rockstar Games\Grand Theft Auto V" 'Rockstar Games'
    # Уникальные пути.
    $seen = @{}
    return $list | Where-Object { $k = $_.Path.ToLowerInvariant(); if ($seen.ContainsKey($k)) { $false } else { $seen[$k] = 1; $true } }
}

function Get-GameVersion($dir) { try { return (Get-Item (Join-Path $dir 'GTA5.exe')).VersionInfo.FileVersion } catch { return '' } }

function Resolve-Game {
    if ($GtaDir) {
        if (-not (Test-Path (Join-Path $GtaDir 'GTA5.exe'))) {
            if (Test-Path (Join-Path $GtaDir 'GTA5_Enhanced.exe')) { Fail 'Это GTA V Enhanced — FloV:MP работает с GTA V Legacy (в Epic Games / Steam / Rockstar это отдельная игра «Grand Theft Auto V Legacy»).' 3 }
            Fail "В папке $GtaDir нет GTA5.exe." 2
        }
        return [pscustomobject]@{ Path = (Resolve-Path $GtaDir).Path.TrimEnd('\'); Source = 'указана вручную' }
    }
    $state = Get-State
    if ($state -and $state.GtaDir -and (Test-Path (Join-Path $state.GtaDir 'GTA5.exe'))) {
        return [pscustomobject]@{ Path = $state.GtaDir; Source = 'прошлая установка' }
    }
    $all = @(Get-GameCandidates)
    $good = @($all | Where-Object { (Get-GameVersion $_.Path) -eq $GameVersion })
    if ($good.Count -ge 1) { return $good[0] }
    if ($all.Count -ge 1) {
        $v = Get-GameVersion $all[0].Path
        Fail "Найдена GTA V ($($all[0].Path)) версии $v, а нужна Legacy $GameVersion. Обновите игру в лаунчере ($($all[0].Source))." 3
    }
    Fail 'GTA V Legacy не найдена. Укажите папку игры: install-client.cmd -GtaDir "D:\Games\GTAV"' 2
}

# --- BattlEye: штатный параметр запуска Rockstar в args.txt ------------------------
function Set-NoBattlEye($dir, [bool]$on) {
    $file = Join-Path $dir 'args.txt'
    $lines = @()
    if (Test-Path $file) { $lines = @(Get-Content $file -Encoding UTF8) }
    $has = [bool]($lines | Where-Object { $_ -match '(^|\s)-nobattleye(\s|$)' })
    if ($on -and -not $has) {
        $lines += '-nobattleye'
        [IO.File]::WriteAllLines($file, [string[]]$lines, (New-Object Text.UTF8Encoding $false))
        return $true
    }
    if (-not $on -and $has) {
        $clean = @($lines | ForEach-Object { ($_ -replace '(^|\s)-nobattleye(?=\s|$)', '').Trim() } | Where-Object { $_ })
        if ($clean.Count -eq 0) { Remove-Item $file -Force } else { [IO.File]::WriteAllLines($file, [string[]]$clean, (New-Object Text.UTF8Encoding $false)) }
        return $true
    }
    return $false
}

function Set-StraightToGame($dir, [bool]$on) {
    # Стартовая страница GTA («Войти / GTA Online / Сюжетный режим») появляется
    # до запуска любых скриптов, поэтому закрыть её из клиента невозможно —
    # игрок просто застревал на ней. С -scOfflineOnly Social Club не выходит в
    # сеть, и игра сразу идёт в одиночный режим, где клиент забирает игрока на
    # сервер под своим загрузочным экраном. Файлы игры не меняются: только
    # commandline.txt, и только нашей строкой (удаление её убирает).
    $file = Join-Path $dir 'commandline.txt'
    $lines = @()
    if (Test-Path $file) { $lines = @(Get-Content $file -Encoding UTF8) }
    $has = [bool]($lines | Where-Object { $_ -match '(^|\s)-scOfflineOnly(\s|$)' })
    if ($on -and -not $has) {
        if (Test-Path $file) { Copy-Item $file (Join-Path $dir 'commandline.flovmp-backup.txt') -Force -ErrorAction SilentlyContinue }
        $lines += '-scOfflineOnly'
        [IO.File]::WriteAllLines($file, [string[]]$lines, (New-Object Text.UTF8Encoding $false))
        return $true
    }
    if (-not $on -and $has) {
        $clean = @($lines | ForEach-Object { ($_ -replace '(^|\s)-scOfflineOnly(?=\s|$)', '').Trim() } | Where-Object { $_ })
        if ($clean.Count -eq 0) { Remove-Item $file -Force } else { [IO.File]::WriteAllLines($file, [string[]]$clean, (New-Object Text.UTF8Encoding $false)) }
        Remove-Item (Join-Path $dir 'commandline.flovmp-backup.txt') -Force -ErrorAction SilentlyContinue
        return $true
    }
    return $false
}

function Get-ServerInfo {
    $f = Join-Path $here 'server.txt'
    $info = @{ address = ''; name = 'FloV:MP' }
    if (Test-Path $f) {
        foreach ($l in Get-Content $f -Encoding UTF8) {
            if ($l -match '^\s*address\s*=\s*(\S+)') { $info.address = $Matches[1] }
            if ($l -match '^\s*name\s*=\s*(.+?)\s*$') { $info.name = $Matches[1] }
        }
    }
    return $info
}

function New-Shortcut($server) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $safe = ($server.name -replace '[\\/:*?"<>|]', '').Trim()
    if (-not $safe) { $safe = 'FloV:MP' }
    $lnk = Join-Path $desktop "$safe.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($lnk)
    $sc.TargetPath = Join-Path $here 'play.cmd'
    $sc.WorkingDirectory = $here
    $sc.Description = "FloV:MP — $($server.name)"
    $gtaIcon = Join-Path $script:game.Path 'GTA5.exe'
    if (Test-Path $gtaIcon) { $sc.IconLocation = "$gtaIcon,0" }
    $sc.Save()
    return $lnk
}

# --- удаление ------------------------------------------------------------------------
if ($Uninstall) {
    $state = Get-State
    $dir = if ($GtaDir) { $GtaDir } elseif ($state) { $state.GtaDir } else { $null }
    if (-not $dir -or -not (Test-Path $dir)) { Fail 'Не знаю, куда был установлен клиент: укажите -GtaDir.' 2 }
    if (-not (Test-Writable $dir)) { Restart-Elevated }
    if (Get-Process GTA5 -ErrorAction SilentlyContinue) { Fail 'Закройте GTA V и повторите.' 6 }
    Remove-Item (Join-Path $dir 'FloVMP.asi') -Force -ErrorAction SilentlyContinue
    if ($state -and $state.InstalledScriptHook) {
        foreach ($f in 'ScriptHookV.dll', 'dinput8.dll') { Remove-Item (Join-Path $dir $f) -Force -ErrorAction SilentlyContinue }
        Say 'ScriptHookV удалён (его ставил установщик FloV:MP).'
    }
    if ($state -and $state.AddedNoBattlEye) { Set-NoBattlEye $dir $false | Out-Null; Say 'BattlEye снова включён (убран -nobattleye из args.txt).' }
    if ($state -and $state.AddedStraightToGame) { Set-StraightToGame $dir $false | Out-Null; Say 'Стартовая страница GTA возвращена (убран -scOfflineOnly).' }
    if ($state -and $state.Shortcut) { Remove-Item $state.Shortcut -Force -ErrorAction SilentlyContinue }
    Remove-Item $StateFile -Force -ErrorAction SilentlyContinue
    Say 'Клиент FloV:MP удалён. Игра в исходном состоянии.' Green
    Finish 0
}

# --- установка -----------------------------------------------------------------------
$script:game = Resolve-Game
$dir = $script:game.Path
Say "GTA V Legacy: $dir ($($script:game.Source))"
$ver = Get-GameVersion $dir
if ($ver -ne $GameVersion) { Fail "Нужна GTA V Legacy $GameVersion, у вас $ver. Обновите игру в лаунчере." 3 }
if (-not (Test-Writable $dir)) { Restart-Elevated }
if (Get-Process GTA5 -ErrorAction SilentlyContinue) { Fail 'GTA V запущена — закройте игру и повторите установку.' 6 }

$state = Get-State
if (-not $state -or $state.GtaDir -ne $dir) { $state = [pscustomobject]@{ GtaDir = $dir; InstalledScriptHook = $false; AddedNoBattlEye = $false; Shortcut = '' } }

# ScriptHookV
$shv = Join-Path $dir 'ScriptHookV.dll'
$shvOk = $false
if (Test-Path $shv) {
    $major = 0; [int]::TryParse((((Get-Item $shv).VersionInfo.FileVersion) -split '\.')[0], [ref]$major) | Out-Null
    $shvOk = $major -ge 3889
    if (-not $shvOk) { Say "ScriptHookV устарел ($((Get-Item $shv).VersionInfo.FileVersion)) — обновляю." Yellow }
}
$loaderOk = (Test-Path (Join-Path $dir 'dinput8.dll')) -or (Test-Path (Join-Path $dir 'xinput1_4.dll'))
if (-not ($shvOk -and $loaderOk)) {
    if (-not $Yes) {
        $answer = Read-Host "Скачать ScriptHookV с официального сайта $ShvPage и установить? [Y/n]"
        if ($answer -match '^[NnНн]') { Fail 'Установите ScriptHookV вручную: ScriptHookV.dll и dinput8.dll из папки bin архива — в папку игры.' 4 }
    }
    $tmp = Join-Path $env:TEMP ('flovmp-scripthookv-' + [guid]::NewGuid().ToString('N') + '.zip')
    Say 'Скачиваю ScriptHookV...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $headers = @{ 'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36'; 'Referer' = $ShvPage }
    $ok = $false
    for ($try = 1; $try -le 3 -and -not $ok; $try++) {
        try { Invoke-WebRequest -Uri $ShvUrl -Headers $headers -OutFile $tmp -UseBasicParsing -TimeoutSec 60; $ok = $true }
        catch { Say "Попытка $try не удалась: $($_.Exception.Message)" Yellow; Start-Sleep 2 }
    }
    if (-not $ok) { Fail "Не удалось скачать ScriptHookV. Проверьте интернет или скачайте вручную: $ShvPage" 4 }
    $hash = (Get-FileHash $tmp -Algorithm SHA256).Hash
    if ($hash -ne $ShvSha256) {
        Remove-Item $tmp -Force
        Fail "Архив ScriptHookV не совпал с проверенным (SHA-256 $hash). Скачайте вручную: $ShvPage" 4
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($tmp)
    try {
        foreach ($name in 'ScriptHookV.dll', 'dinput8.dll') {
            $entry = $zip.Entries | Where-Object { ($_.FullName -replace '\\', '/') -like "*bin/$name" } | Select-Object -First 1
            if (-not $entry) { throw "в архиве нет $name" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $dir $name), $true)
        }
    } finally { $zip.Dispose(); Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
    $state.InstalledScriptHook = $true
    Say 'ScriptHookV установлен.' Green
} else {
    Say "ScriptHookV на месте ($((Get-Item $shv).VersionInfo.FileVersion))."
}

# BattlEye
if ($KeepBattlEye) {
    Say 'BattlEye оставлен как есть (-KeepBattlEye): включите «без BattlEye» в настройках лаунчера Rockstar сами.' Yellow
} elseif (Set-NoBattlEye $dir $true) {
    $state.AddedNoBattlEye = $true
    Say 'Режим «без BattlEye» включён (args.txt папки игры). GTA Online в нём недоступна; вернуть — install-client.cmd -Uninstall.' Green
} else {
    Say 'Режим «без BattlEye» уже включён.'
}

# Вход в игру без стартовой страницы GTA
if (Set-StraightToGame $dir $true) {
    $state.AddedStraightToGame = $true
    Say 'Игра будет запускаться сразу в мир, без стартовой страницы GTA.' Green
} else {
    Say 'Запуск сразу в мир уже настроен.'
}

# Клиент
$asi = Join-Path $here 'FloVMP.asi'
if (-not (Test-Path $asi)) { Fail 'Рядом с установщиком нет FloVMP.asi — распакуйте архив клиента полностью.' 5 }
Copy-Item $asi (Join-Path $dir 'FloVMP.asi') -Force
Say 'Клиент FloV:MP установлен.' Green

# Ярлык на сервер
$server = Get-ServerInfo
if (-not $NoShortcut -and $server.address) {
    try { $state.Shortcut = New-Shortcut $server; Say "Ярлык на рабочем столе: «$($server.name)»." Green }
    catch { Say "Ярлык не создан: $($_.Exception.Message)" Yellow }
}
Save-State $state

Write-Host ''
if ($server.address) { Say "Готово. Играть: ярлык «$($server.name)» на рабочем столе (или play.cmd)." Green }
else { Say 'Готово. Играть: play.cmd <адрес сервера>, или в игре клавиша F9.' Green }
Say 'После запуска игры нажмите «Сюжетный режим» — клиент сам подключится к серверу.'
Finish 0
