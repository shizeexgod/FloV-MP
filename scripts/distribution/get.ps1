param(
    [Parameter(Mandatory = $true)] [string]$Key,
    [string]$InstallDir = 'C:\FloVMP',
    [string]$Dist = '',
    [switch]$Force,
    [switch]$NoStart
)
# FloV:MP — установка сервера одной командой (Windows).
#
#   powershell -ExecutionPolicy Bypass -Command "iwr http://<адрес раздачи>/cdn/get.ps1 -OutFile flovmp-get.ps1; .\flovmp-get.ps1 -Key FLV-XXXX-XXXX-XXXX"
#
# По ключу лицензии получает описание последнего релиза, проверяет его подпись
# ключом релизов FloV:MP (вшит ниже), скачивает ZIP-пакет, сверяет SHA-256 и
# запускает установщик из пакета (install.ps1). Подмена пакета не пройдёт.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue' # иначе Invoke-WebRequest качает в разы медленнее

if (-not $Dist) { $Dist = if ($env:FLOVMP_DIST_URL) { $env:FLOVMP_DIST_URL } else { 'http://188.127.229.224' } }
$Dist = $Dist.TrimEnd('/')

# BEGIN RELEASE_PUBKEY_XML
$ReleasePubKeyXml = '<RSAKeyValue><Modulus>pmynUrPAKz17KYFCg3URy5BgBanGtIDxbLIExHQ59tdxAcUN2uPMN7Wu51TSkvit5kKxZvDWF9MSll2sLCXUJMpX9Lxa1GFLpmx6axrrw44z4id0ESrb1C7kqyOYu54lBVdBVyCup09Kgyfrc1vE9J7LRTUD+9DaahJ1CVkcg4uopbBItVqywb4UuOlbGuAf1x/ocgO3hrKv9e6R+LN33EH3udfMlEcq7GWVN5/GW0709KWF21zemEm4wS3NRUWnAGvwBcwgOIQdKoybjZtMgY6rpKCganSdpcGthunHnAOddbPSoerR6imGgqL2zTzKMFsPpxxOz1Mop3MdqkDbM5g2laOt8CyfcTwYgQi2OUf7K3MmXqkR1sv8duIlRRW1GOEaMg7M2zvm4MwVe4qMIms7ME+fREEIAE4UmdP8v5Pc8fhEQNl5WD2eTXY57DC08IrRE0GbwAg1R99C9Du3mtDfUsL1Uo+uwzLny0LnQuhILpMotkDPe7arcMbsSVQp</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>'
# END RELEASE_PUBKEY_XML

function Fail($t) { Write-Host "ОШИБКА: $t" -ForegroundColor Red; exit 1 }
if (-not $ReleasePubKeyXml) { Fail 'в загрузчике нет ключа релизов — скачайте get.ps1 заново' }
$cleanKey = ($Key.Trim().ToUpperInvariant() -replace '[^A-Z0-9-]', '')
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

    Write-Host '==> Релиз FloV:MP' -ForegroundColor Cyan
    $releaseFile = Join-Path $work 'release.txt'
    Fetch "$Dist/api/v1/distribution/release?$q" $releaseFile
    Fetch "$Dist/api/v1/distribution/release.sig?$q" (Join-Path $work 'release.sig')
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

    Write-Host '==> Скачивание пакета' -ForegroundColor Cyan
    $zip = Join-Path $work $info.file
    Fetch "$Dist/api/v1/distribution/download?$q" $zip
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
    $arguments = @{ InstallDir = $InstallDir; LicenseKey = $cleanKey }
    if ($Force) { $arguments.Force = $true }
    if ($NoStart) { $arguments.NoStart = $true }
    & $installer @arguments
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
