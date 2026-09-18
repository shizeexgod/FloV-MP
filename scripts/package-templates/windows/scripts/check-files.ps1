# Проверка файлов платформы по manifest.txt.
#
# Зачем: после ручного копирования, распаковки поверх или сбоя диска половина
# проблем выглядит одинаково — «сервер не запускается». Эта проверка отвечает
# на первый вопрос: файлы платформы целы или нет. Ваши файлы (настройки, свой
# сервер, данные) в манифесте не перечислены и не проверяются.
#
# Запуск: scripts\check-files.cmd

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$root = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $root 'manifest.txt'

if (-not (Test-Path $manifest)) {
    Write-Host "[FloV:MP] Не найден manifest.txt — распакуйте пакет полностью." -ForegroundColor Red
    exit 1
}

$total = 0
$missing = @()
$changed = @()

foreach ($line in Get-Content $manifest -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split '\s+', 2
    if ($parts.Count -lt 2) { continue }

    $expected = $parts[0].Trim()
    $rel = $parts[1].Trim()
    $path = Join-Path $root $rel
    $total++

    if (-not (Test-Path $path)) { $missing += $rel; continue }
    $actual = (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected.ToLowerInvariant()) { $changed += $rel }
}

Write-Host "[FloV:MP] Проверено файлов платформы: $total"

if ($missing.Count -gt 0) {
    Write-Host "Отсутствуют ($($missing.Count)):" -ForegroundColor Red
    $missing | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
}
if ($changed.Count -gt 0) {
    Write-Host "Изменены ($($changed.Count)):" -ForegroundColor Yellow
    $changed | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
    Write-Host "Если вы правили их намеренно — это ожидаемо. Иначе распакуйте пакет заново."
}
if ($missing.Count -eq 0 -and $changed.Count -eq 0) {
    Write-Host "Все файлы платформы на месте и не изменены." -ForegroundColor Green
    exit 0
}
exit 1
