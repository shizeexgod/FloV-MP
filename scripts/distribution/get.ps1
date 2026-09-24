param(
    [string]$Key = '',
    [string]$InstallDir = '',
    [string]$Dist = '',
    [string]$GitHub = '',
    [string]$Tag = '',
    [switch]$Force,
    [switch]$NoStart,
    [switch]$Reinstall
)
# FloV:MP — установка и обновление сервера одной командой (Windows).
#
#   cd C:\Мой сервер
#   powershell -ExecutionPolicy Bypass -Command "iwr http://<адрес>/cdn/get.ps1 -OutFile get.ps1; .\get.ps1 -Key FLV-XXXX-XXXX-XXXX-XXXX"
#
# Без -InstallDir ставит в текущую папку (ту, куда клиент зашёл через cd).
# Повторный запуск в той же папке — обновление: ключ и настройки берутся из
# уже установленного config\flovmp.env, свои файлы клиента не трогаются.
#
# По ключу лицензии получает описание последнего релиза, проверяет его подпись
# ключом релизов FloV:MP (вшит ниже), скачивает ZIP-пакет, сверяет SHA-256 и
# запускает установщик из пакета (install.ps1). Подмена пакета не пройдёт.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue' # иначе Invoke-WebRequest качает в разы медленнее

if (-not $Dist) { $Dist = if ($env:FLOVMP_DIST_URL) { $env:FLOVMP_DIST_URL } else { 'http://188.127.229.224' } }
$Dist = $Dist.TrimEnd('/')
# -GitHub owner/repo — брать релиз из GitHub, а не с сервера раздачи. Подпись
# проверяется та же самая, поэтому подменить пакет на GitHub тоже нельзя.
# Без -Tag берётся последний стабильный релиз; с -Tag — именно он (например
# бета: -Tag v1.0.5-beta). Так у клиентов остаётся стабильная версия, а
# владелец может поставить бету одной и той же командой.
$GitHubBase = if ($GitHub) {
    $repo = $GitHub.Trim('/')
    if ($Tag) { "https://github.com/$repo/releases/download/$($Tag.Trim())" }
    else { "https://github.com/$repo/releases/latest/download" }
} else { '' }

# BEGIN RELEASE_PUBKEY_XML
$ReleasePubKeyXml = '<RSAKeyValue><Modulus>pmynUrPAKz17KYFCg3URy5BgBanGtIDxbLIExHQ59tdxAcUN2uPMN7Wu51TSkvit5kKxZvDWF9MSll2sLCXUJMpX9Lxa1GFLpmx6axrrw44z4id0ESrb1C7kqyOYu54lBVdBVyCup09Kgyfrc1vE9J7LRTUD+9DaahJ1CVkcg4uopbBItVqywb4UuOlbGuAf1x/ocgO3hrKv9e6R+LN33EH3udfMlEcq7GWVN5/GW0709KWF21zemEm4wS3NRUWnAGvwBcwgOIQdKoybjZtMgY6rpKCganSdpcGthunHnAOddbPSoerR6imGgqL2zTzKMFsPpxxOz1Mop3MdqkDbM5g2laOt8CyfcTwYgQi2OUf7K3MmXqkR1sv8duIlRRW1GOEaMg7M2zvm4MwVe4qMIms7ME+fREEIAE4UmdP8v5Pc8fhEQNl5WD2eTXY57DC08IrRE0GbwAg1R99C9Du3mtDfUsL1Uo+uwzLny0LnQuhILpMotkDPe7arcMbsSVQp</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>'
# END RELEASE_PUBKEY_XML

function Fail($t) { Write-Host "ОШИБКА: $t" -ForegroundColor Red; exit 1 }
if (-not $ReleasePubKeyXml) { Fail 'в загрузчике нет ключа релизов — скачайте get.ps1 заново' }

# Куда ставим: по умолчанию — текущая папка (клиент зашёл в неё через cd).
if (-not $InstallDir) { $InstallDir = (Get-Location).ProviderPath }
$target = [IO.Path]::GetFullPath($InstallDir).TrimEnd([IO.Path]::DirectorySeparatorChar)
foreach ($forbidden in @($env:SystemRoot, (Join-Path $env:SystemRoot 'System32'), $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:USERPROFILE)) {
    if ($forbidden -and $target.TrimEnd('\').Equals($forbidden.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        Fail "в системную папку ставить нельзя: $target. Зайдите в папку сервера (cd) или укажите -InstallDir"
    }
}
Write-Host "Папка установки: $target" -ForegroundColor Cyan

# Обновление: ключ уже лежит рядом, второй раз его вводить не нужно.
$envPath = Join-Path $target 'config\flovmp.env'
$installedVersion = ''
if (Test-Path -LiteralPath (Join-Path $target 'manifest.json')) {
    try { $installedVersion = (Get-Content -LiteralPath (Join-Path $target 'manifest.json') -Raw | ConvertFrom-Json).version } catch { }
}
if (-not $Key -and (Test-Path -LiteralPath $envPath)) {
    foreach ($line in Get-Content -LiteralPath $envPath -Encoding UTF8) {
        if ($line -match '^\s*FLOVMP_LICENSE_KEY\s*=\s*(\S+)') { $Key = $Matches[1] }
    }
    if ($Key) { Write-Host '  ключ лицензии взят из установленного config\flovmp.env' }
}
if (-not $Key) { Fail 'нужен ключ лицензии: -Key FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX' }

$cleanKey = ($Key.Trim().ToUpperInvariant() -replace '[^A-Z0-9-]', '')
if ($cleanKey -notmatch '^FLV(-[0-9A-F]{8}){4}$') { Fail "ключ не похож на лицензионный: $cleanKey" }
$q = "os=windows&key=$cleanKey"

$work = Join-Path ([IO.Path]::GetTempPath()) ('flovmp-get-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
try {
    function Fetch([string]$Url, [string]$Out) {
        try { Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Out -TimeoutSec 600 }
        catch {
            $msg = $_.Exception.Message
            try { $msg = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { }
            Fail "сервер раздачи: $msg"
        }
    }

    # Пакет — десятки мегабайт, а у части провайдеров длинные загрузки
    # обрываются. Докачиваем с места обрыва (HTTP Range: GitHub и nginx раздачи
    # его поддерживают); сервер без Range — качаем целиком заново.
    # Invoke-WebRequest в Windows PowerShell 5.1 докачивать не умеет.
    function Download([string]$Url, [string]$Out) {
        if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Force }
        $resume = $true
        for ($try = 1; $try -le 8; $try++) {
            $have = if (Test-Path -LiteralPath $Out) { (Get-Item -LiteralPath $Out).Length } else { 0 }
            $response = $null
            try {
                $request = [Net.HttpWebRequest]::Create($Url)
                $request.Timeout = 30000
                $request.ReadWriteTimeout = 60000
                $request.UserAgent = 'FloVMP-get'
                if ($resume -and $have -gt 0) { $request.AddRange([long]$have) }
                try { $response = $request.GetResponse() }
                catch [Net.WebException] {
                    $r = $_.Exception.Response
                    if ($r) {
                        $code = [int]$r.StatusCode
                        $r.Close()
                        if ($code -eq 416) { return }   # уже скачан целиком — проверит SHA-256
                        if ($code -ge 400) { Fail "$Url ответил $code" }
                    }
                    throw
                }
                $append = $have -gt 0 -and [int]$response.StatusCode -eq 206
                if ($have -gt 0 -and -not $append -and $resume) {
                    Write-Host '  сервер не поддерживает докачку — качаю пакет заново' -ForegroundColor Yellow
                    $resume = $false
                }
                $mode = if ($append) { [IO.FileMode]::Append } else { [IO.FileMode]::Create }
                $before = if ($append) { $have } else { 0 }
                $expected = $response.ContentLength
                $file = New-Object IO.FileStream($Out, $mode, [IO.FileAccess]::Write)
                try { $response.GetResponseStream().CopyTo($file) } finally { $file.Dispose(); $response.Close() }
                $got = (Get-Item -LiteralPath $Out).Length - $before
                if ($expected -ge 0 -and $got -lt $expected) { throw 'поток закрылся раньше конца файла' }
                return
            }
            catch {
                if ($response) { $response.Close() }
                $mb = if (Test-Path -LiteralPath $Out) { [math]::Floor((Get-Item -LiteralPath $Out).Length / 1MB) } else { 0 }
                Write-Host "  связь оборвалась на $mb МБ — докачиваю (попытка $($try + 1) из 8)" -ForegroundColor Yellow
                Start-Sleep -Seconds ($try * 2)
            }
        }
        Fail "не удалось скачать ${Url}: связь рвётся. Повторите позже"
    }

    Write-Host '==> Релиз FloV:MP' -ForegroundColor Cyan
    $releaseFile = Join-Path $work 'release.txt'
    if ($GitHubBase) {
        Fetch "$GitHubBase/release-windows.txt" $releaseFile
        Fetch "$GitHubBase/release-windows.txt.sig" (Join-Path $work 'release.sig')
    } else {
        Fetch "$Dist/api/v1/distribution/release?$q" $releaseFile
        Fetch "$Dist/api/v1/distribution/release.sig?$q" (Join-Path $work 'release.sig')
    }
    $releaseBytes = [IO.File]::ReadAllBytes($releaseFile)
    $sig = [Convert]::FromBase64String(([IO.File]::ReadAllText((Join-Path $work 'release.sig'))).Trim())
    $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider
    $rsa.FromXmlString($ReleasePubKeyXml)
    if (-not $rsa.VerifyData($releaseBytes, [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'), $sig)) {
        Fail 'подпись релиза НЕ верна — описание подменено. Установка остановлена'
    }
    Write-Host '  подпись релиза верна' -ForegroundColor Green

    $info = @{}
    foreach ($line in [Text.Encoding]::UTF8.GetString($releaseBytes) -split "`n") {
        $i = $line.IndexOf('='); if ($i -gt 0) { $info[$line.Substring(0, $i)] = $line.Substring($i + 1).Trim() }
    }
    if ($info.os -ne 'windows') { Fail "релиз не для Windows ($($info.os))" }
    if ($info.file -notmatch '^[A-Za-z0-9._-]+\.zip$') { Fail "неверное имя пакета: $($info.file)" }
    if ($info.sha256 -notmatch '^[0-9a-f]{64}$') { Fail 'неверный SHA-256 в релизе' }
    Write-Host "  версия $($info.version), пакет $($info.file)"

    if ($installedVersion) {
        Write-Host "  установлено сейчас: $installedVersion"
        if ($installedVersion -eq $info.version -and -not $Reinstall) {
            Write-Host 'Обновление не требуется: установлена та же версия. Повторить установку — ключ -Reinstall.' -ForegroundColor Green
            exit 0
        }
    }

    Write-Host '==> Скачивание пакета' -ForegroundColor Cyan
    $zip = Join-Path $work $info.file
    if ($GitHubBase) { Download "$GitHubBase/$($info.file)" $zip }
    else { Download "$Dist/api/v1/distribution/download?$q" $zip }
    $got = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($got -ne $info.sha256) { Fail "SHA-256 пакета не совпал (ожидали $($info.sha256), получили $got)" }
    Write-Host '  SHA-256 совпал' -ForegroundColor Green

    $unpack = Join-Path $work 'package'
    Expand-Archive -LiteralPath $zip -DestinationPath $unpack -Force
    $roots = @(Get-ChildItem -LiteralPath $unpack -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.txt') })
    if ($roots.Count -ne 1) { Fail 'в пакете должен быть ровно один каталог с manifest.txt' }
    $installer = Join-Path $roots[0].FullName 'install.ps1'
    if (-not (Test-Path -LiteralPath $installer)) { Fail 'в пакете нет install.ps1' }

    Write-Host '==> Установка' -ForegroundColor Cyan
    $arguments = @{ InstallDir = $target; LicenseKey = $cleanKey }
    if ($Force) { $arguments.Force = $true }
    if ($NoStart) { $arguments.NoStart = $true }
    try { & $installer @arguments }
    catch {
        # Установщик бросает понятное сообщение (столкновение файлов, нет места,
        # файл занят). Клиенту нужна причина, а не трассировка PowerShell.
        Fail $_.Exception.Message
    }
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
