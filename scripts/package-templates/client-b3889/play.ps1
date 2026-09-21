param([Parameter(Mandatory = $true)][string]$Address, [string]$Name = "")
# Подключиться к серверу FloV:MP: оставить клиенту запрос и запустить GTA V.
# Address — адрес игрового сервера (1.2.3.4 или 1.2.3.4:7788); клиент сам
# подключится к шлюзу игроков (порт игры + 10) после загрузки сюжетного режима.
$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:LOCALAPPDATA 'FloVMP'
New-Item -ItemType Directory -Force $dir | Out-Null

$hostPart = $Address; $port = 7788
if ($Address -match '^(?<h>[^:]+):(?<p>\d+)$') { $hostPart = $Matches.h; $port = [int]$Matches.p }
$native = "{0}:{1}" -f $hostPart, ($port + 10)
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
[IO.File]::WriteAllText((Join-Path $dir 'connect.txt'), "address=$native`nname=$Name`ncreated=$now`n", (New-Object Text.UTF8Encoding $false))
Write-Host "[FloV:MP] Сервер: $native. Запускаю GTA V — клиент подключится после загрузки сюжетного режима."

if (Get-Process GTA5 -ErrorAction SilentlyContinue) {
    Write-Host '[FloV:MP] GTA V уже запущена: нажмите F9 в игре.' -ForegroundColor Yellow
    exit 0
}
$gta = ''
$saved = Join-Path $dir 'gta-dir.txt'
if (Test-Path $saved) { $gta = (Get-Content $saved -Encoding UTF8 | Select-Object -First 1).Trim() }
$epic = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V' -ErrorAction SilentlyContinue).InstallFolderEpic
if ($epic -and $gta -and ((Resolve-Path $epic).Path -eq (Resolve-Path $gta).Path)) {
    # Epic Games: запуск через лаунчер Epic (он передаёт игре лицензию).
    Start-Process 'com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true'
} elseif ($gta -and (Test-Path (Join-Path $gta 'steam_api64.dll'))) {
    Start-Process 'steam://rungameid/271590'
} elseif ($gta -and (Test-Path (Join-Path $gta 'PlayGTAV.exe'))) {
    Start-Process (Join-Path $gta 'PlayGTAV.exe') -WorkingDirectory $gta
} else {
    Write-Host '[FloV:MP] Не знаю, где игра: запустите install-client.cmd, затем повторите, или запустите GTA V сами — клиент подключится.' -ForegroundColor Yellow
}
