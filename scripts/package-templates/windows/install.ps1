[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallDir = 'C:\FloVMP',
    [switch]$Force,
    [switch]$NoStart,
    [string]$LicenseKey = '',
    [string]$LicenseUrl = '', # адрес сервера лицензий FloV:MP (по умолчанию — вшитый в сервер)
    [string]$PortalUrl = '',  # устарел, оставлен для совместимости со старыми командами
    [switch]$SkipRuntime      # не ставить .NET 10 (машина без интернета: поставьте его вручную)
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$source = [IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Definition))
$manifestPath = Join-Path $source 'manifest.txt'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Не найден manifest.txt. Запускайте install.cmd из распакованного пакета.' }

function Get-SafeRelativePath([string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative.IndexOf([char]0) -ge 0) { throw "Недопустимый путь в манифесте: '$Relative'" }
    $p = $Relative.Replace('/', '\')
    if ([IO.Path]::IsPathRooted($p) -or $p -match ':') { throw "Абсолютный или ADS-путь запрещён: $Relative" }
    $parts = $p -split '\\'
    if ($parts | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' }) { throw "Небезопасный путь в манифесте: $Relative" }
    return ($parts -join '\')
}

function Get-ContainedPath([string]$Root, [string]$Relative) {
    $safe = Get-SafeRelativePath $Relative
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath((Join-Path $Root $safe))
    if (-not $full.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Путь выходит за пределы каталога: $Relative" }
    return $full
}

function Read-Manifest([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Не найден manifest.txt: $Path" }
    $result = @()
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([0-9a-fA-F]{64})\s{2,}(.+)$') { throw "Неверная строка manifest.txt: $line" }
        $hash = $Matches[1].ToLowerInvariant(); $rel = Get-SafeRelativePath $Matches[2].Trim()
        if ($result | Where-Object { $_.Path.Equals($rel, [StringComparison]::OrdinalIgnoreCase) }) { throw "Повторный путь в manifest.txt: $rel" }
        $result += [pscustomobject]@{ Hash = $hash; Path = $rel }
    }
    if ($result.Count -eq 0) { throw "manifest.txt пуст: $Path" }
    return $result
}

function Set-FlovmpEnvValue([string]$Path, [string]$Key, [string]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $lines = [Collections.Generic.List[string]]::new()
        foreach ($line in [IO.File]::ReadAllLines($Path)) { [void]$lines.Add($line) }
    } else {
        $example = Join-Path $source 'config\flovmp.env.example'
        $lines = [Collections.Generic.List[string]]::new()
        if (Test-Path -LiteralPath $example -PathType Leaf) {
            foreach ($line in [IO.File]::ReadAllLines($example)) { [void]$lines.Add($line) }
        }
    }
    $prefix = $Key + '='
    $found = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith($prefix, [StringComparison]::Ordinal)) {
            $lines[$i] = $prefix + $Value
            $found = $true
        }
    }
    if (-not $found) { [void]$lines.Add($prefix + $Value) }
    [IO.File]::WriteAllLines($Path, $lines, [Text.UTF8Encoding]::new($false))
}

# Сервер с 1.0.7 собран под .NET 10: без него не запустится ни FloVMP-Server.exe,
# ни ресурс платформы. У клиентов, ставивших 1.0.6 и раньше, стоит .NET 8, и
# обновление без этой проверки оставляло их с сервером, который не стартует.
$DotnetMajor = 10
function Test-DotnetRuntime {
    $roots = @($env:DOTNET_ROOT, (Join-Path $env:ProgramFiles 'dotnet')) | Where-Object { $_ }
    foreach ($root in $roots) {
        $dir = Join-Path $root 'shared\Microsoft.NETCore.App'
        if (Test-Path -LiteralPath $dir) {
            $found = Get-ChildItem -LiteralPath $dir -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^(\d+)\.' -and [int]$Matches[1] -eq $DotnetMajor } | Select-Object -First 1
            if ($found) { return (Join-Path $dir $found.Name) }
        }
    }
    return $null
}

function Install-DotnetRuntime {
    $have = Test-DotnetRuntime
    if ($have) { Write-Host "  .NET $DotnetMajor на месте: $have" -ForegroundColor Green; return }
    if ($SkipRuntime) {
        throw (".NET $DotnetMajor Runtime x64 не найден, а установка отключена (-SkipRuntime). " +
               "Ничего не изменено. Поставьте его: https://dotnet.microsoft.com/download/dotnet/$DotnetMajor.0")
    }
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin) {
        throw (".NET $DotnetMajor Runtime x64 не найден, а для его установки нужны права администратора. " +
               'Ничего не изменено — запустите установку от имени администратора.')
    }
    Write-Host "  Ставлю .NET $DotnetMajor Runtime (официальный скрипт Microsoft)..." -ForegroundColor Yellow
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $script = Join-Path ([IO.Path]::GetTempPath()) ('dotnet-install-' + [Guid]::NewGuid().ToString('N') + '.ps1')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
        & $script -Channel "$DotnetMajor.0" -Runtime dotnet -InstallDir (Join-Path $env:ProgramFiles 'dotnet') | Out-Null
    }
    catch { throw "Не удалось поставить .NET $DotnetMajor ($($_.Exception.Message)). Ничего не изменено." }
    finally { Remove-Item -LiteralPath $script -Force -ErrorAction SilentlyContinue }
    $have = Test-DotnetRuntime
    if (-not $have) { throw ".NET $DotnetMajor поставлен, но не найден в $env:ProgramFiles\dotnet. Ничего не изменено." }
    Write-Host "  .NET $DotnetMajor поставлен: $have" -ForegroundColor Green
}

$entries = @(Read-Manifest $manifestPath)

Write-Host "[1/4] Проверка исходного пакета ($($entries.Count) файлов)..." -ForegroundColor Cyan
foreach ($entry in $entries) {
    $src = Get-ContainedPath $source $entry.Path
    if (-not (Test-Path -LiteralPath $src -PathType Leaf)) { throw "Отсутствует файл пакета: $($entry.Path)" }
    $actual = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Hash) { throw "SHA-256 не совпал: $($entry.Path)" }
}
Write-Host '  OK: пакет целый.' -ForegroundColor Green

# До любых изменений в папке сервера: если рантайм не встал, у клиента
# остаётся рабочая прежняя версия, а не наполовину обновлённый сервер.
Write-Host "Проверка .NET $DotnetMajor..." -ForegroundColor Cyan
Install-DotnetRuntime

$target = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
$targetIsOurs = Test-Path -LiteralPath (Join-Path $target 'manifest.txt') -PathType Leaf
$targetNotEmpty = (Test-Path -LiteralPath $target) -and (Get-ChildItem -LiteralPath $target -Force | Select-Object -First 1)

# В папке клиента может уже лежать чужой сервер или мод. Платформа трогает
# только свои файлы из manifest.txt, поэтому вместо запрета «папка не пустая»
# ищем настоящие столкновения: чужие файлы с теми же именами.
$conflicts = @()
if ($targetNotEmpty -and -not $targetIsOurs) {
    foreach ($entry in $entries) {
        $existing = Get-ContainedPath $target $entry.Path
        if (Test-Path -LiteralPath $existing -PathType Leaf) { $conflicts += $entry.Path }
    }
    if ($conflicts.Count -gt 0 -and -not $Force) {
        $shown = ($conflicts | Select-Object -First 10) -join "`n  "
        $more = if ($conflicts.Count -gt 10) { "`n  ... и ещё $($conflicts.Count - 10)" } else { '' }
        throw ("В папке $target уже есть чужие файлы с такими же именами:`n  $shown$more`n" +
               'Ничего не изменено. Поставьте в пустую папку (-InstallDir) или повторите с -Force: ' +
               'тогда эти файлы сначала уйдут в резервную копию, а потом будут заменены.')
    }
    Write-Host "  Папка не пуста: ставим рядом, посторонние файлы не трогаем." -ForegroundColor Yellow
}

$parent = Split-Path -Parent $target
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$stage = Join-Path $parent ('.flovmp-stage-' + [Guid]::NewGuid().ToString('N'))
$backup = Join-Path $parent ('.flovmp-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$targetExisted = Test-Path -LiteralPath $target
$oldEntries = @()
$committed = $false
try {
    Write-Host '[2/4] Подготовка временной копии...' -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    foreach ($entry in $entries) {
        $src = Get-ContainedPath $source $entry.Path
        $dst = Get-ContainedPath $stage $entry.Path
        New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }

    if (-not $PSCmdlet.ShouldProcess($target, 'установить проверенную платформу')) { return }
    Write-Host '[3/4] Резервная копия старой платформы и установка...' -ForegroundColor Cyan
    if (Test-Path -LiteralPath (Join-Path $target 'manifest.txt')) {
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        $oldEntries = @(Read-Manifest (Join-Path $target 'manifest.txt'))
        foreach ($entry in $oldEntries) {
            $oldPath = Get-ContainedPath $target $entry.Path
            if (Test-Path -LiteralPath $oldPath -PathType Leaf) {
                $backupPath = Get-ContainedPath $backup $entry.Path
                New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
                Copy-Item -LiteralPath $oldPath -Destination $backupPath -Force
            }
        }
        Copy-Item -LiteralPath (Join-Path $target 'manifest.txt') -Destination (Join-Path $backup 'manifest.txt') -Force
        if (Test-Path -LiteralPath (Join-Path $target 'manifest.json')) { Copy-Item (Join-Path $target 'manifest.json') (Join-Path $backup 'manifest.json') -Force }

        # Удаляем только платформенные файлы, которые исчезли из новой версии.
        # Пользовательские файлы в manifest.txt не перечисляются и потому не
        # затрагиваются. Резервная копия уже создана выше, поэтому catch может
        # восстановить и удалённые устаревшие файлы.
        $newPaths = @{}
        foreach ($entry in $entries) { $newPaths[$entry.Path.ToLowerInvariant()] = $true }
        foreach ($entry in $oldEntries) {
            if (-not $newPaths.ContainsKey($entry.Path.ToLowerInvariant())) {
                $obsolete = Get-ContainedPath $target $entry.Path
                if (Test-Path -LiteralPath $obsolete -PathType Leaf) {
                    Remove-Item -LiteralPath $obsolete -Force
                }
            }
        }
    }
    elseif ($conflicts.Count -gt 0) {
        # -Force поверх чужой папки: каждый чужой файл уходит в копию до замены.
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        foreach ($rel in $conflicts) {
            $backupPath = Get-ContainedPath $backup $rel
            New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
            Copy-Item -LiteralPath (Get-ContainedPath $target $rel) -Destination $backupPath -Force
        }
        Write-Host "  Чужие файлы ($($conflicts.Count)) сохранены в $backup" -ForegroundColor Yellow
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach ($entry in $entries) {
        $src = Get-ContainedPath $stage $entry.Path
        $dst = Get-ContainedPath $target $entry.Path
        New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $target 'manifest.txt') -Force
    if (Test-Path -LiteralPath (Join-Path $source 'manifest.json')) { Copy-Item (Join-Path $source 'manifest.json') (Join-Path $target 'manifest.json') -Force }

    Write-Host '[4/4] Повторная проверка установленной платформы...' -ForegroundColor Cyan
    foreach ($entry in $entries) {
        $dst = Get-ContainedPath $target $entry.Path
        $actual = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $entry.Hash) { throw "Проверка после копирования не пройдена: $($entry.Path)" }
    }
    if (-not [string]::IsNullOrWhiteSpace($LicenseKey)) {
        Set-FlovmpEnvValue (Join-Path $target 'config\flovmp.env') 'FLOVMP_LICENSE_KEY' $LicenseKey
        Write-Host 'Лицензионный ключ сохранён в config\flovmp.env.' -ForegroundColor Green

        # Ключ активирует сам сервер при запуске (он знает ID своей установки).
        Write-Host 'Сервер активирует ключ при первом запуске FloVMP-Server.exe.' -ForegroundColor Green
        $url = if ($LicenseUrl) { $LicenseUrl } else { $PortalUrl }
        if ($url) {
            Set-FlovmpEnvValue (Join-Path $target 'config\flovmp.env') 'FLOVMP_LICENSE_URL' $url.TrimEnd('/')
            Write-Host "Сервер лицензий: $url" -ForegroundColor Green
        }
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $target 'license.flv'))) {
        Write-Host 'Ключ лицензии не указан. После запуска сервера введите в его окне: license activate FLV-...' -ForegroundColor Yellow
    }
    Write-Host "Готово: $target" -ForegroundColor Green
    $committed = $true
    if ($backup -and (Test-Path $backup)) { Write-Host "Резервная копия: $backup" -ForegroundColor Yellow }
    if (-not $NoStart) { Write-Host 'Запуск не выполняется автоматически. Проверьте настройки и запустите FloVMP-Server.exe.' }
}
catch {
    Write-Host "Ошибка установки: $($_.Exception.Message)" -ForegroundColor Red
    if ($oldEntries.Count -gt 0 -and (Test-Path -LiteralPath (Join-Path $backup 'manifest.txt'))) {
        foreach ($entry in $entries) {
            $newPath = Get-ContainedPath $target $entry.Path
            if (Test-Path -LiteralPath $newPath -PathType Leaf) { Remove-Item -LiteralPath $newPath -Force -ErrorAction SilentlyContinue }
        }
        foreach ($entry in $oldEntries) {
            $oldPath = Get-ContainedPath $backup $entry.Path
            if (Test-Path -LiteralPath $oldPath -PathType Leaf) {
                $restorePath = Get-ContainedPath $target $entry.Path
                New-Item -ItemType Directory -Path (Split-Path -Parent $restorePath) -Force | Out-Null
                Copy-Item -LiteralPath $oldPath -Destination $restorePath -Force
            }
        }
        Copy-Item (Join-Path $backup 'manifest.txt') (Join-Path $target 'manifest.txt') -Force
        if (Test-Path -LiteralPath (Join-Path $backup 'manifest.json')) { Copy-Item (Join-Path $backup 'manifest.json') (Join-Path $target 'manifest.json') -Force }
        Write-Host "Прежняя версия восстановлена из $backup" -ForegroundColor Yellow
    }
    elseif (-not $targetExisted -and (Test-Path -LiteralPath $target)) {
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
