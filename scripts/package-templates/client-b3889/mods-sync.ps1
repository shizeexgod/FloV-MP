param(
    [string]$GtaDir = "",
    [string]$Address = "",
    [string]$Source = "",
    [switch]$Yes,
    [switch]$Elevated
)
# Моды сервера FloV:MP в папку игры (пункт 4 roadmap). Запускается из play.cmd
# перед стартом игры — GTA читает моды только при запуске.
#
#   mods-sync.ps1 -Address 1.2.3.4:7788            — моды этого сервера
#   mods-sync.ps1 -Source https://cdn.example/mods — явно, со своего CDN
#
# Откуда брать (по порядку): -Source; строка mods= в server.txt рядом;
# адрес, который сервер сообщил клиенту при прошлом входе; сам игровой
# сервер (порт игры + 20).
#
# Папкой GTA\mods, пока игрок играет через FloV:MP, управляет FloV:MP: свои
# моды игрока при первом запуске убираются в сторону (mods.player-<дата>)
# и возвращаются при удалении клиента (uninstall-client.cmd).
#
# Качаются только данные игры: исполняемые файлы (.asi, .dll, .exe, скрипты)
# не принимаются, даже если сервер их пришлёт. Каждый файл сверяется по
# SHA-256, оборванная загрузка докачивается.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$DataDir = Join-Path $env:LOCALAPPDATA 'FloVMP'
$StateFile = Join-Path $DataDir 'install.json'
$SourcesFile = Join-Path $DataDir 'mods-sources.txt'
New-Item -ItemType Directory -Force (Join-Path $DataDir 'logs') | Out-Null
$LogFile = Join-Path $DataDir 'logs\mods.log'

function Say($t, $c = 'Gray') {
    Write-Host "[FloV:MP] $t" -ForegroundColor $c
    try { Add-Content -Path $LogFile -Value ("{0:HH:mm:ss} {1}" -f (Get-Date), $t) -Encoding UTF8 } catch { }
}
function Finish($code) { if ($Elevated) { Read-Host 'Нажмите Enter, чтобы закрыть окно' | Out-Null }; exit $code }

# Те же правила, что у сервера (ModManifest.ValidPath): данные игры, без «..»,
# без диска и потоков NTFS. Сервер, который пришлёт .asi, получит отказ здесь.
$Allowed = @('.rpf', '.ytd', '.ydr', '.ydd', '.yft', '.ybn', '.ymap', '.ytyp', '.ymt', '.ymf', '.ycd', '.ynv', '.ynd',
    '.ypt', '.ypdb', '.yld', '.ysc', '.awc', '.rel', '.gxt2', '.meta', '.xml', '.dat', '.dat151', '.dat54',
    '.dat4', '.dat22', '.dat10', '.nametable', '.txt', '.json', '.ide', '.ipl', '.cut', '.png', '.jpg')
function Test-ModPath([string]$p) {
    if (-not $p -or $p.Length -gt 240 -or $p.StartsWith('/') -or $p.EndsWith('/')) { return $false }
    if ($p -match '[\x00-\x1f\\:*?"<>|]') { return $false }
    foreach ($part in $p.Split('/')) {
        if ($part.Length -eq 0 -or $part -eq '.' -or $part -eq '..' -or $part.EndsWith('.') -or $part.EndsWith(' ')) { return $false }
    }
    return $Allowed -contains ([IO.Path]::GetExtension($p).ToLowerInvariant())
}

function Get-Sha256Hex([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Get-FileSha([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }

# --- игра -----------------------------------------------------------------------
$state = $null
if (Test-Path $StateFile) { try { $state = Get-Content $StateFile -Raw -Encoding UTF8 | ConvertFrom-Json } catch { } }
if (-not $GtaDir -and $state) { $GtaDir = $state.GtaDir }
if (-not $GtaDir -or -not (Test-Path (Join-Path $GtaDir 'GTA5.exe'))) { Say 'Не знаю, где игра: сначала install-client.cmd.' Red; Finish 2 }
$ModsDir = Join-Path $GtaDir 'mods'
$Marker = Join-Path $ModsDir '.flovmp-mods.json'

# --- откуда качать -----------------------------------------------------------------
function Read-KeyFile([string]$path, [string]$key) {
    if (-not (Test-Path $path)) { return '' }
    foreach ($l in Get-Content $path -Encoding UTF8) { if ($l -match ('^\s*' + [regex]::Escape($key) + '\s*=\s*(\S+)')) { return $Matches[1] } }
    return ''
}
if (-not $Address) { $Address = Read-KeyFile (Join-Path $here 'server.txt') 'address' }
if (-not $Source) { $Source = Read-KeyFile (Join-Path $here 'server.txt') 'mods' }
# Клиент пишет адрес модов по строке MODS с ключом «хост:порт игры».
$key = if ($Address -match ':\d+$') { $Address } elseif ($Address) { "${Address}:7788" } else { '' }
if (-not $Source -and $key) { $Source = Read-KeyFile $SourcesFile $key }
$fallback = $false
if (-not $Source -and $Address) {
    $h = $Address; $port = 7788
    if ($Address -match '^(?<h>[^:]+):(?<p>\d+)$') { $h = $Matches.h; $port = [int]$Matches.p }
    $Source = "http://{0}:{1}/mods" -f $h, ($port + 20)
    $fallback = $true
}
if (-not $Source) { Say 'Адрес сервера не указан — моды не проверяю.' Yellow; Finish 0 }
$Source = $Source.TrimEnd('/')
if ($Source -notmatch '^https?://') { Say "Неверный адрес модов: $Source" Red; Finish 2 }

# --- список модов --------------------------------------------------------------------
function Get-Text([string]$url) {
    $req = [Net.HttpWebRequest]::Create($url)
    $req.Timeout = 15000; $req.ReadWriteTimeout = 30000; $req.UserAgent = 'FloVMP-mods'
    $resp = $req.GetResponse()
    try { $r = New-Object IO.StreamReader($resp.GetResponseStream(), [Text.Encoding]::UTF8); return $r.ReadToEnd() } finally { $resp.Close() }
}
try { $json = Get-Text "$Source/manifest.json" }
catch {
    # Сервер без модов не открывает порт раздачи — это нормально.
    if ($fallback) { Say 'У сервера нет модов для скачивания.'; Finish 0 }
    Say "Список модов не получен ($Source): $($_.Exception.Message)" Yellow
    Say 'Игра запустится со старыми модами; если серверу они нужны, он скажет об этом при входе.' Yellow
    Finish 4
}
$manifest = $json | ConvertFrom-Json
if ($manifest.format -ne 1) { Say 'Список модов в неизвестном формате — обновите клиент FloV:MP.' Red; Finish 4 }
$files = @($manifest.files)
$lines = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) {
    if (-not (Test-ModPath $f.path) -or [long]$f.size -lt 0 -or $f.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        Say "Сервер прислал недопустимый файл «$($f.path)» — моды не установлены (защита от чужого кода)." Red
        Finish 5
    }
    $lines.Add(("{0}`t{1}`t{2}`n" -f $f.path, [long]$f.size, $f.sha256.ToLowerInvariant()))
}
# Отпечаток — как у сервера (ModManifest.ComputeDigest): строки по пути, порядок ordinal.
$sorted = $lines.ToArray(); [Array]::Sort($sorted, [StringComparer]::Ordinal)
if ((Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes(($sorted -join '')))) -ne $manifest.digest) {
    Say 'Список модов повреждён (отпечаток не совпал) — моды не установлены.' Red; Finish 5
}
if ($files.Count -eq 0) { Say 'У сервера нет модов.'; Finish 0 }

$local = $null
if (Test-Path $Marker) { try { $local = Get-Content $Marker -Raw -Encoding UTF8 | ConvertFrom-Json } catch { } }
if ($local -and $local.digest -eq $manifest.digest) { Say "Моды сервера на месте ($($files.Count) файлов)." Green; Finish 0 }

$total = ($files | Measure-Object -Property size -Sum).Sum
Say ("Моды сервера: {0} файлов, {1:0.#} ГБ — проверяю, что уже есть..." -f $files.Count, ($total / 1GB))

# --- права и папка --------------------------------------------------------------
function Test-Writable($dir) {
    try { $probe = Join-Path $dir ('.flovmp-write-' + [guid]::NewGuid().ToString('N')); [IO.File]::WriteAllText($probe, 'x'); Remove-Item $probe -Force; return $true }
    catch { return $false }
}
if (-not (Test-Writable $GtaDir)) {
    if ($Elevated) { Say 'Нет прав на запись в папку игры даже от администратора.' Red; Finish 7 }
    Say 'Папка игры защищена Windows — нужны права администратора, перезапускаю...' Yellow
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-GtaDir', "`"$GtaDir`"", '-Source', "`"$Source`"", '-Elevated')
    if ($Yes) { $argList += '-Yes' }
    try { $p = Start-Process powershell.exe -ArgumentList $argList -Verb RunAs -Wait -PassThru; exit $p.ExitCode }
    catch { Say 'Без прав администратора моды не поставить. Запустите play.cmd правой кнопкой → «Запуск от имени администратора».' Red; exit 7 }
}
if (Get-Process GTA5 -ErrorAction SilentlyContinue) { Say 'GTA V запущена — закройте игру: моды меняются только до её запуска.' Red; Finish 6 }

# Свои моды игрока — в сторону (решение владельца: папкой mods управляет FloV:MP).
if ((Test-Path $ModsDir) -and -not (Test-Path $Marker) -and (Get-ChildItem -LiteralPath $ModsDir -Force | Select-Object -First 1)) {
    $backup = Join-Path $GtaDir ('mods.player-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Move-Item -LiteralPath $ModsDir -Destination $backup
    Say "Ваши моды перенесены в $backup — вернутся при удалении клиента (uninstall-client.cmd)." Yellow
    if (-not $state) { $state = [pscustomobject]@{ GtaDir = $GtaDir } }
    $state | Add-Member -NotePropertyName PlayerModsBackup -NotePropertyValue $backup -Force
    $state | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8
}
New-Item -ItemType Directory -Force $ModsDir | Out-Null

# --- что качать ------------------------------------------------------------------
$known = @{}
if ($local -and $local.files) { foreach ($p in $local.files.PSObject.Properties) { $known[$p.Name] = $p.Value } }
$todo = New-Object System.Collections.Generic.List[object]
$have = @{}
foreach ($f in $files) {
    $path = Join-Path $ModsDir ($f.path.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $sha = $f.sha256.ToLowerInvariant()
    if (Test-Path -LiteralPath $path) {
        $info = Get-Item -LiteralPath $path
        $k = $known[$f.path]
        # Файл не менялся с прошлой проверки (размер и время те же) — хэш не пересчитываем.
        $current = if ($k -and [long]$k.size -eq $info.Length -and [long]$k.ticks -eq $info.LastWriteTimeUtc.Ticks) { $k.sha } else { $null }
        if (-not $current -and $info.Length -eq [long]$f.size) { $current = Get-FileSha $path }
        if ($current -eq $sha) { $have[$f.path] = @{ size = $info.Length; ticks = $info.LastWriteTimeUtc.Ticks; sha = $sha }; continue }
    }
    $todo.Add($f)
}
$need = ($todo | Measure-Object -Property size -Sum).Sum
if (-not $need) { $need = 0 }
$drive = New-Object IO.DriveInfo ([IO.Path]::GetPathRoot($GtaDir))
if ($drive.AvailableFreeSpace -lt ($need * 1.05 + 200MB)) {
    Say ("Не хватает места на диске {0}: нужно {1:0.#} ГБ, свободно {2:0.#} ГБ." -f $drive.Name, ($need / 1GB), ($drive.AvailableFreeSpace / 1GB)) Red
    Finish 8
}

# --- загрузка с докачкой ----------------------------------------------------------
function Get-File([string]$url, [string]$out, [string]$etag) {
    $part = "$out.part"
    for ($try = 1; $try -le 8; $try++) {
        $haveBytes = if (Test-Path -LiteralPath $part) { (Get-Item -LiteralPath $part).Length } else { 0 }
        $resp = $null
        try {
            $req = [Net.HttpWebRequest]::Create($url)
            $req.Timeout = 30000; $req.ReadWriteTimeout = 60000; $req.UserAgent = 'FloVMP-mods'
            if ($haveBytes -gt 0) { $req.AddRange([long]$haveBytes); $req.Headers.Add('If-Range', "`"$etag`"") }
            try { $resp = $req.GetResponse() }
            catch [Net.WebException] {
                $r = $_.Exception.Response
                if ($r) { $code = [int]$r.StatusCode; $r.Close(); if ($code -eq 416) { return }; if ($code -ge 400 -and $code -ne 503) { throw "сервер ответил $code" } }
                throw
            }
            $append = $haveBytes -gt 0 -and [int]$resp.StatusCode -eq 206
            $mode = if ($append) { [IO.FileMode]::Append } else { [IO.FileMode]::Create }
            $before = if ($append) { $haveBytes } else { 0 }
            $expected = $resp.ContentLength
            $fs = New-Object IO.FileStream($part, $mode, [IO.FileAccess]::Write)
            try { $resp.GetResponseStream().CopyTo($fs) } finally { $fs.Dispose(); $resp.Close() }
            if ($expected -ge 0 -and ((Get-Item -LiteralPath $part).Length - $before) -lt $expected) { throw 'поток закрылся раньше конца файла' }
            return
        }
        catch {
            if ($resp) { $resp.Close() }
            if ("$_" -like 'сервер ответил*') { throw }
            Say ("  связь оборвалась — докачиваю (попытка {0} из 8)" -f ($try + 1)) Yellow
            Start-Sleep -Seconds ($try * 2)
        }
    }
    throw 'связь рвётся — повторите позже'
}

$done = 0; $doneBytes = 0
foreach ($f in $todo) {
    $done++
    $path = Join-Path $ModsDir ($f.path.Replace('/', [IO.Path]::DirectorySeparatorChar))
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    $url = $Source + '/files/' + (($f.path.Split('/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/')
    Say ("[{0}/{1}] {2} ({3:0.#} МБ)" -f $done, $todo.Count, $f.path, ($f.size / 1MB))
    try { Get-File $url $path $f.sha256.ToLowerInvariant() }
    catch { Say "Не скачан $($f.path): $_" Red; Finish 9 }
    $got = Get-FileSha "$path.part"
    if ($got -ne $f.sha256.ToLowerInvariant()) {
        Remove-Item -LiteralPath "$path.part" -Force
        Say "Файл $($f.path) скачан с ошибкой (SHA-256 не совпал) — запустите play.cmd ещё раз." Red
        Finish 9
    }
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    Move-Item -LiteralPath "$path.part" -Destination $path
    $info = Get-Item -LiteralPath $path
    $have[$f.path] = @{ size = $info.Length; ticks = $info.LastWriteTimeUtc.Ticks; sha = $got }
    $doneBytes += $f.size
}

# Файлы прошлого набора, которых в новом нет, — убрать (только наши, по отметке).
$wanted = @{}; foreach ($f in $files) { $wanted[$f.path] = $true }
foreach ($old in $known.Keys) {
    if (-not $wanted[$old]) {
        $p = Join-Path $ModsDir ($old.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force; Say "  убран устаревший $old" }
    }
}

$markerData = [ordered]@{ format = 1; digest = $manifest.digest; source = $Source; files = $have }
[IO.File]::WriteAllText($Marker, ($markerData | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding $false))
Say ("Моды сервера установлены: скачано {0} файлов, {1:0.#} МБ." -f $todo.Count, ($doneBytes / 1MB)) Green

# Моды из папки mods игра читает только через OpenIV.asi.
if (-not (Test-Path (Join-Path $GtaDir 'OpenIV.asi'))) {
    Say 'Чтобы игра увидела моды, нужен OpenIV.asi (его ставит программа OpenIV, отдельно он не раздаётся):' Yellow
    Say '  1) скачайте OpenIV с официального сайта openiv.com и установите;' Yellow
    Say '  2) в OpenIV: Tools → ASI Manager → OpenIV.asi → Install (ASI Loader уже стоит — его ставит FloV:MP).' Yellow
    Say '  3) снова запустите play.cmd.' Yellow
    if (-not $Yes) { try { Start-Process 'https://openiv.com/' } catch { } }
    Finish 10
}
Finish 0
