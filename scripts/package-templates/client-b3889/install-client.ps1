param(
    [string]$GtaDir = "",
    [switch]$Yes
)
# Установка клиента FloV:MP в GTA V Legacy 1.0.3889.0.
#   1) находит игру (Epic, Steam, Rockstar Games Launcher) или берёт -GtaDir;
#   2) проверяет, что это Legacy 1.0.3889.0;
#   3) ставит ScriptHookV (скачивает с официального сайта dev-c.com и сверяет
#      SHA-256; распространять ScriptHookV в своём архиве автор запрещает);
#   4) кладёт FloVMP.asi в папку игры.
# Файлы самой игры (GTA5.exe, *.rpf) не изменяются.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$GameVersion = '1.0.3889.0'
$ShvUrl = 'http://www.dev-c.com/files/ScriptHookV_3889.0_1158.13.zip'
$ShvPage = 'http://www.dev-c.com/gtav/scripthookv/'
$ShvSha256 = 'B64C97C3353906F14621E7E9511E4AEC2A7D436ECC21ED124D3816585E2E6188'

function Say($t, $c = 'Gray') { Write-Host "[FloV:MP] $t" -ForegroundColor $c }

function Find-Gta {
    $candidates = @()
    foreach ($k in 'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V', 'HKLM:\SOFTWARE\Rockstar Games\Grand Theft Auto V') {
        $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
        if ($p) { $candidates += $p.InstallFolderEpic, $p.InstallFolderSteam, $p.InstallFolder }
    }
    $steam = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 271590' -ErrorAction SilentlyContinue
    if ($steam) { $candidates += $steam.InstallLocation }
    $candidates += "$env:ProgramFiles\Epic Games\GTAV", "$env:ProgramFiles\Rockstar Games\Grand Theft Auto V",
                   "${env:ProgramFiles(x86)}\Steam\steamapps\common\Grand Theft Auto V"
    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c 'GTA5.exe'))) { return (Resolve-Path $c).Path }
    }
    return $null
}

if (-not $GtaDir) { $GtaDir = Find-Gta }
if (-not $GtaDir -or -not (Test-Path (Join-Path $GtaDir 'GTA5.exe'))) {
    Say 'GTA V не найдена автоматически. Укажите папку: install-client.cmd -GtaDir "D:\Games\GTAV"' Red
    exit 2
}
$exe = Join-Path $GtaDir 'GTA5.exe'
$ver = (Get-Item $exe).VersionInfo.FileVersion
Say "GTA V: $GtaDir (версия $ver)"
if ($ver -ne $GameVersion) {
    Say "Нужна GTA V Legacy $GameVersion. Обновите игру в своём лаунчере (Enhanced-версия не поддерживается)." Red
    exit 3
}

# --- ScriptHookV ---------------------------------------------------------------
$shv = Join-Path $GtaDir 'ScriptHookV.dll'
$shvOk = $false
if (Test-Path $shv) {
    $major = [int](((Get-Item $shv).VersionInfo.FileVersion -split '\.')[0])
    $shvOk = $major -ge 3889
    if (-not $shvOk) { Say "ScriptHookV устарел ($((Get-Item $shv).VersionInfo.FileVersion)) — нужен 3889 или новее." Yellow }
}
$loaderOk = (Test-Path (Join-Path $GtaDir 'dinput8.dll')) -or (Test-Path (Join-Path $GtaDir 'xinput1_4.dll'))
if (-not ($shvOk -and $loaderOk)) {
    if (-not $Yes) {
        $answer = Read-Host "Скачать ScriptHookV с официального сайта $ShvPage и установить? (Y/N)"
        if ($answer -notmatch '^[YyДд]') { Say "Установите ScriptHookV вручную: скопируйте ScriptHookV.dll и dinput8.dll из папки bin архива в папку игры." Yellow; exit 4 }
    }
    $tmp = Join-Path $env:TEMP 'flovmp-scripthookv.zip'
    Say 'Скачиваю ScriptHookV...'
    $headers = @{ 'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36'; 'Referer' = $ShvPage }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $ShvUrl -Headers $headers -OutFile $tmp -UseBasicParsing
    $hash = (Get-FileHash $tmp -Algorithm SHA256).Hash
    if ($hash -ne $ShvSha256) {
        Remove-Item $tmp -Force
        Say "Архив ScriptHookV не совпал с проверенным (SHA-256 $hash). Установите вручную с $ShvPage" Red
        exit 4
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($tmp)
    try {
        foreach ($name in 'ScriptHookV.dll', 'dinput8.dll') {
            $entry = $zip.Entries | Where-Object { $_.FullName -replace '\\', '/' -like "*bin/$name" } | Select-Object -First 1
            if (-not $entry) { throw "в архиве нет $name" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $GtaDir $name), $true)
        }
    } finally { $zip.Dispose(); Remove-Item $tmp -Force }
    Say 'ScriptHookV установлен.' Green
} else {
    Say "ScriptHookV на месте ($((Get-Item $shv).VersionInfo.FileVersion))."
}

# --- клиент FloV:MP --------------------------------------------------------------
Copy-Item (Join-Path $here 'FloVMP.asi') (Join-Path $GtaDir 'FloVMP.asi') -Force
New-Item -ItemType Directory -Force (Join-Path $env:LOCALAPPDATA 'FloVMP') | Out-Null
Set-Content -Path (Join-Path $env:LOCALAPPDATA 'FloVMP\gta-dir.txt') -Value $GtaDir -Encoding UTF8 -ErrorAction SilentlyContinue
Say 'Клиент FloV:MP установлен.' Green
Say 'Игра с модами работает в сюжетном режиме без BattlEye: в Rockstar Games Launcher →'
Say 'Настройки → GTA V → «Аргументы запуска» впишите -nobattleye (GTA Online при этом недоступна).'
Say 'Подключение: play.cmd <адрес сервера>, или в игре клавиша F9.' Green
