[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallDir = 'C:\FloVMP',
    [switch]$Force,
    [switch]$NoStart
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

$entries = @()
foreach ($line in Get-Content -LiteralPath $manifestPath -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -notmatch '^([0-9a-fA-F]{64})\s{2,}(.+)$') { throw "Неверная строка manifest.txt: $line" }
    $hash = $Matches[1].ToLowerInvariant(); $rel = Get-SafeRelativePath $Matches[2].Trim()
    if ($entries | Where-Object { $_.Path -eq $rel }) { throw "Повторный путь в manifest.txt: $rel" }
    $entries += [pscustomobject]@{ Hash = $hash; Path = $rel }
}
if ($entries.Count -eq 0) { throw 'manifest.txt пуст.' }

Write-Host "[1/4] Проверка исходного пакета ($($entries.Count) файлов)..." -ForegroundColor Cyan
foreach ($entry in $entries) {
    $src = Get-ContainedPath $source $entry.Path
    if (-not (Test-Path -LiteralPath $src -PathType Leaf)) { throw "Отсутствует файл пакета: $($entry.Path)" }
    $actual = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Hash) { throw "SHA-256 не совпал: $($entry.Path)" }
}
Write-Host '  OK: пакет целый.' -ForegroundColor Green

$target = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
if ((Test-Path -LiteralPath $target) -and (Get-ChildItem -LiteralPath $target -Force | Select-Object -First 1) -and
    (-not (Test-Path -LiteralPath (Join-Path $target 'manifest.txt'))) -and (-not $Force)) {
    throw "Целевая папка не похожа на FloV:MP: $target. Используйте -Force или другую -InstallDir."
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
        $old = Get-Content -LiteralPath (Join-Path $target 'manifest.txt') -Encoding UTF8
        foreach ($line in $old) {
            if ($line -notmatch '^([0-9a-fA-F]{64})\s{2,}(.+)$') { continue }
            $oldEntries += [pscustomobject]@{ Hash = $Matches[1].ToLowerInvariant(); Path = (Get-SafeRelativePath $Matches[2].Trim()) }
            $oldPath = Get-ContainedPath $target $oldEntries[-1].Path
            if (Test-Path -LiteralPath $oldPath -PathType Leaf) {
                $backupPath = Get-ContainedPath $backup $oldEntries[-1].Path
                New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
                Copy-Item -LiteralPath $oldPath -Destination $backupPath -Force
            }
        }
        Copy-Item -LiteralPath (Join-Path $target 'manifest.txt') -Destination (Join-Path $backup 'manifest.txt') -Force
        if (Test-Path -LiteralPath (Join-Path $target 'manifest.json')) { Copy-Item (Join-Path $target 'manifest.json') (Join-Path $backup 'manifest.json') -Force }
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
