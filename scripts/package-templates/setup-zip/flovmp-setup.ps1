param(
    [string]$Key = '',
    [string]$InstallDir = '',
    [string]$Dist = '',
    [string]$GitHub = '',   # owner/repo: брать релиз с GitHub вместо сервера раздачи
    [switch]$Reinstall,
    [switch]$Quiet   # без вопросов: для планировщика и автообновления
)
# FloV:MP — установка и обновление в один клик.
#
# Лежит рядом с get.ps1 (загрузчик с вшитым ключом релизов). Этот файл только
# спрашивает у владельца сервера ключ лицензии и папку, запоминает их в
# «настройки.txt» и передаёт работу загрузчику. Повторный запуск = обновление.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
$settingsPath = Join-Path $here 'настройки.txt'
$loader = Join-Path $here 'get.ps1'

function Fail($t) { Write-Host "ОШИБКА: $t" -ForegroundColor Red; exit 1 }
if (-not (Test-Path -LiteralPath $loader)) { Fail 'рядом нет get.ps1 — распакуйте архив целиком, не по одному файлу' }

Write-Host ''
Write-Host '  FloV:MP — установка и обновление сервера' -ForegroundColor Cyan
Write-Host '  ----------------------------------------' -ForegroundColor Cyan

# 1. Настройки прошлого запуска: ключ и адрес сервера лицензий.
$saved = @{}
if (Test-Path -LiteralPath $settingsPath) {
    foreach ($line in Get-Content -LiteralPath $settingsPath -Encoding UTF8) {
        if ($line -match '^\s*([^#=]+?)\s*=\s*(.*)$') { $saved[$Matches[1].Trim().ToLowerInvariant()] = $Matches[2].Trim() }
    }
}
if (-not $Key) { $Key = $saved['ключ'] }
if (-not $Dist) { $Dist = $saved['адрес'] }
if (-not $GitHub) { $GitHub = $saved['github'] }
if (-not $InstallDir) { $InstallDir = $saved['папка'] }

# 2. Папка сервера. По умолчанию — та, куда распаковали архив.
if (-not $InstallDir) {
    $InstallDir = $here
    $name = Split-Path -Leaf $here
    $risky = @('Downloads', 'Загрузки', 'Desktop', 'Рабочий стол', 'Documents', 'Документы', 'Temp')
    if (-not $Quiet) {
        if ($risky -contains $name) {
            Write-Host ''
            Write-Host "  Внимание: архив распакован в «$name». Сервер лучше поставить в отдельную папку," -ForegroundColor Yellow
            Write-Host '  например C:\FloVMP — тогда его не заденет уборка загрузок.' -ForegroundColor Yellow
        }
        Write-Host ''
        Write-Host "  Папка сервера [$InstallDir]"
        $answer = Read-Host '  Enter — оставить, либо введите другой путь'
        if ($answer.Trim()) { $InstallDir = $answer.Trim().Trim('"') }
    }
}

# 3. Ключ лицензии: спрашиваем один раз, дальше берём из настроек.
if (-not $Key -and -not $Quiet) {
    Write-Host ''
    Write-Host '  Ключ лицензии выдаётся при покупке и выглядит так:'
    Write-Host '  FLV-1A2B3C4D-5E6F7A8B-9C0D1E2F-3A4B5C6D' -ForegroundColor DarkGray
    $Key = (Read-Host '  Ключ лицензии').Trim()
}
if (-not $Key) {
    # Обновление уже установленного сервера: ключ лежит в его настройках.
    $envPath = Join-Path $InstallDir 'config\flovmp.env'
    if (Test-Path -LiteralPath $envPath) {
        foreach ($line in Get-Content -LiteralPath $envPath -Encoding UTF8) {
            if ($line -match '^\s*FLOVMP_LICENSE_KEY\s*=\s*(\S+)') { $Key = $Matches[1] }
        }
    }
}
if (-not $Key) { Fail 'без ключа лицензии установить нельзя' }

# 4. Запоминаем, чтобы следующий запуск был обновлением в один клик.
$lines = @(
    '# Настройки установки FloV:MP. Файл читается при каждом запуске.',
    '# Ключ лицензии — ваш, никому его не передавайте.',
    "ключ = $Key",
    "папка = $InstallDir"
)
if ($Dist) { $lines += "адрес = $Dist" }
if ($GitHub) { $lines += "github = $GitHub" }
[IO.File]::WriteAllLines($settingsPath, $lines, [Text.UTF8Encoding]::new($true))

# 5. Работу делает загрузчик: подпись релиза, SHA-256 пакета, установка.
$arguments = @{ Key = $Key; InstallDir = $InstallDir }
if ($Dist) { $arguments.Dist = $Dist }
if ($GitHub) { $arguments.GitHub = $GitHub }
if ($Reinstall) { $arguments.Reinstall = $true }
Write-Host ''
& $loader @arguments
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host '  Дальше:' -ForegroundColor Cyan
Write-Host "  1. запустите сервер: $InstallDir\FloVMP-Server.exe"
Write-Host '  2. настройки сервера — server\server.toml и server\config\client.cfg'
Write-Host '  3. обновление — просто запустите этот же файл ещё раз'
