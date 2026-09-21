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
$reg = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V' -ErrorAction SilentlyContinue
function Norm($p) { if ($p -and (Test-Path (Join-Path $p 'GTA5.exe'))) { return ([IO.Path]::GetFullPath($p)).TrimEnd([char]92).ToLowerInvariant() } return $null }
$gta = $null
$saved = Join-Path $dir 'gta-dir.txt'
if (Test-Path $saved) { $gta = Norm ((Get-Content $saved -Encoding UTF8 | Select-Object -First 1).Trim()) }
if (-not $gta -and $reg) { $gta = Norm $reg.InstallFolderEpic; if (-not $gta) { $gta = Norm $reg.InstallFolderSteam }; if (-not $gta) { $gta = Norm $reg.InstallFolder } }
$epic = if ($reg) { Norm $reg.InstallFolderEpic } else { $null }

if ($gta -and $epic -and $gta -eq $epic) {
    # Epic Games: запуск через лаунчер Epic (он передаёт игре лицензию).
    Start-Process 'com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true'
} elseif ($gta -and (Test-Path (Join-Path $gta 'steam_api64.dll'))) {
    Start-Process 'steam://rungameid/271590'
} elseif ($gta -and (Test-Path (Join-Path $gta 'PlayGTAV.exe'))) {
    Start-Process (Join-Path $gta 'PlayGTAV.exe') -WorkingDirectory $gta
} else {
    Write-Host '[FloV:MP] GTA V не найдена: запустите install-client.cmd, или запустите игру сами — клиент подключится после загрузки.' -ForegroundColor Yellow
}
