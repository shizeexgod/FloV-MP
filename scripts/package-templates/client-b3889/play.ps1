param([string]$Address = "", [string]$Name = "", [string]$Url = "")
# Подключиться к серверу FloV:MP: оставить клиенту запрос и запустить GTA V.
# Без адреса берётся server.txt рядом (его заполняет владелец сервера).
# Адрес — адрес игрового сервера (1.2.3.4 или 1.2.3.4:7788); клиент сам
# подключится к шлюзу игроков (порт игры + 10) после загрузки сюжетного режима.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dir = Join-Path $env:LOCALAPPDATA 'FloVMP'
New-Item -ItemType Directory -Force $dir | Out-Null
function Say($t, $c = 'Gray') { Write-Host "[FloV:MP] $t" -ForegroundColor $c }

# Ссылка flovmp://1.2.3.4:7788 (или flovmp://connect/1.2.3.4:7788?name=Ник) —
# её открывает кнопка «Играть» в лаунчере или на сайте сервера. Ссылку может
# подсунуть любая страница, поэтому адрес строго проверяется, а игрока
# спрашивают, хочет ли он на этот сервер.
if ($Url) {
    $u = $Url.Trim()
    if ($u -notmatch '^flovmp://(connect/)?(?<addr>[A-Za-z0-9.\-]{1,253}(:\d{1,5})?)/?(\?name=(?<name>[^&]{1,32}))?$') {
        Say "Неверная ссылка на сервер: $u" Red
        exit 2
    }
    $Address = $Matches.addr
    $nameRaw = $Matches.name
    # Порт игры + 10 — шлюз игроков, поэтому верхняя граница 65525.
    if ($Address -match ':(\d+)$' -and ([int]$Matches[1] -lt 1 -or [int]$Matches[1] -gt 65525)) {
        Say "Неверный порт в ссылке: $u" Red
        exit 2
    }
    if ($nameRaw) { $Name = [Uri]::UnescapeDataString($nameRaw) -replace '[^\p{L}\p{N}_ .-]', '' }
    Add-Type -AssemblyName System.Windows.Forms
    $answer = [System.Windows.Forms.MessageBox]::Show("Подключиться к серверу $Address?", 'FloV:MP',
        [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }
}

if (-not $Address) {
    $f = Join-Path $here 'server.txt'
    if (Test-Path $f) { foreach ($l in Get-Content $f -Encoding UTF8) { if ($l -match '^\s*address\s*=\s*(\S+)') { $Address = $Matches[1] } } }
}
if (-not $Address) {
    Say 'Адрес сервера не указан. Пример: play.cmd 1.2.3.4:7788' Yellow
    Say '(или владелец сервера вписывает его в server.txt рядом с play.cmd)' Yellow
    exit 1
}

# Клиент установлен? Лаунчер может передать точную папку игры через env,
# чтобы пакетный вход не выбрал другую Legacy-установку из реестра.
$gta = [Environment]::GetEnvironmentVariable('FLOVMP_GTA_PATH', 'Process')
$state = Join-Path $dir 'install.json'
if (-not $gta -and (Test-Path $state)) { try { $gta = (Get-Content $state -Raw -Encoding UTF8 | ConvertFrom-Json).GtaDir } catch { } }
if (-not $gta -or -not (Test-Path (Join-Path $gta 'FloVMP.asi'))) {
    Say 'Клиент FloV:MP ещё не установлен — запускаю установку...' Yellow
    $installArgs = @('-Yes')
    if ($gta) { $installArgs += @('-GtaDir', $gta) }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'install-client.ps1') @installArgs
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
    # Клиент в игре сам заметит запрос (connect.txt) за секунду и перейдёт на сервер.
    Say "GTA V уже запущена — переход на сервер $Address." Green
    exit 0
}
# Моды сервера (карта, машины) — до запуска: GTA читает папку mods только при старте.
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'mods-sync.ps1') -GtaDir $gta -Address $Address
$modsCode = $LASTEXITCODE
# 0 — моды на месте или их нет; 4 — список не получен (сервер скажет при входе).
if ($modsCode -ne 0 -and $modsCode -ne 4) {
    Say 'Игра не запущена: моды сервера не готовы (причина выше). Исправьте и запустите play.cmd снова.' Red
    exit $modsCode
}
Say "Сервер $Address. Запускаю GTA V..."
Say 'Игра откроется сразу в мире сервера — ничего выбирать не нужно.' Green

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
