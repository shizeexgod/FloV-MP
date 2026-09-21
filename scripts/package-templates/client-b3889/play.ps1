param([string]$Address = "", [string]$Name = "")
# Подключиться к серверу FloV:MP: оставить клиенту запрос и запустить GTA V.
# Без адреса берётся server.txt рядом (его заполняет владелец сервера).
# Адрес — адрес игрового сервера (1.2.3.4 или 1.2.3.4:7788); клиент сам
# подключится к шлюзу игроков (порт игры + 10) после загрузки сюжетного режима.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dir = Join-Path $env:LOCALAPPDATA 'FloVMP'
New-Item -ItemType Directory -Force $dir | Out-Null
function Say($t, $c = 'Gray') { Write-Host "[FloV:MP] $t" -ForegroundColor $c }

if (-not $Address) {
    $f = Join-Path $here 'server.txt'
    if (Test-Path $f) { foreach ($l in Get-Content $f -Encoding UTF8) { if ($l -match '^\s*address\s*=\s*(\S+)') { $Address = $Matches[1] } } }
}
if (-not $Address) {
    Say 'Адрес сервера не указан. Пример: play.cmd 1.2.3.4:7788' Yellow
    Say '(или владелец сервера вписывает его в server.txt рядом с play.cmd)' Yellow
    exit 1
}

# Клиент установлен?
$gta = $null
$state = Join-Path $dir 'install.json'
if (Test-Path $state) { try { $gta = (Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).GtaDir } catch { } }
if (-not $gta -or -not (Test-Path (Join-Path $gta 'FloVMP.asi'))) {
    Say 'Клиент FloV:MP ещё не установлен — запускаю установку...' Yellow
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'install-client.ps1') -Yes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    try { $gta = (Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).GtaDir } catch { }
}

$hostPart = $Address; $port = 7788
if ($Address -match '^(?<h>[^:]+):(?<p>\d+)$') { $hostPart = $Matches.h; $port = [int]$Matches.p }
$native = "{0}:{1}" -f $hostPart, ($port + 10)
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
[IO.File]::WriteAllText((Join-Path $dir 'connect.txt'), "address=$native`nname=$Name`ncreated=$now`n", (New-Object Text.UTF8Encoding $false))
# F9 в игре предложит этот же сервер.
[IO.File]::WriteAllText((Join-Path $dir 'last-server.txt'), "$Address`n$Name`n", (New-Object Text.UTF8Encoding $false))

if (Get-Process GTA5 -ErrorAction SilentlyContinue) {
    Say 'GTA V уже запущена: нажмите F9 в игре — адрес уже подставлен.' Yellow
    exit 0
}
Say "Сервер $Address. Запускаю GTA V..."
Say 'На заставке выберите «Сюжетный режим» — клиент сам зайдёт на сервер.' Green

function Norm($p) { if ($p -and (Test-Path (Join-Path $p 'GTA5.exe'))) { return ([IO.Path]::GetFullPath($p)).TrimEnd([char]92).ToLowerInvariant() } return $null }
$g = Norm $gta
$epicDir = $null
Get-ChildItem 'C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests' -Filter *.item -ErrorAction SilentlyContinue | ForEach-Object {
    try { $j = Get-Content $_.FullName -Raw | ConvertFrom-Json; if ($j.AppName -eq '9d2d0eb64d5c44529cece33fe2a46482') { $epicDir = Norm $j.InstallLocation } } catch { }
}
if ($g -and $epicDir -and $g -eq $epicDir) {
    # Epic Games: запуск через лаунчер Epic (он передаёт игре лицензию).
    Start-Process 'com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true'
} elseif ($g -and (Test-Path (Join-Path $g 'steam_api64.dll'))) {
    Start-Process 'steam://rungameid/271590'
} elseif ($g -and (Test-Path (Join-Path $g 'PlayGTAV.exe'))) {
    Start-Process (Join-Path $g 'PlayGTAV.exe') -WorkingDirectory $g
} else {
    Say 'Не удалось определить лаунчер игры — запустите GTA V сами, клиент подключится после загрузки.' Yellow
}
