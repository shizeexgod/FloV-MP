[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [uri]$PackageUrl,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$Sha256,
    [string]$LicenseKey = '',
    [string]$PortalUrl = '',
    [string]$InstallDir = (Join-Path (Get-Location) 'FloVMP'),
    [switch]$Force,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$work = Join-Path ([IO.Path]::GetTempPath()) ('flovmp-bootstrap-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
try {
    $archive = Join-Path $work 'flovmp-server.zip'
    Write-Host '[1/3] Скачивание проверенного ZIP-пакета...' -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri $PackageUrl -OutFile $archive
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Sha256.ToLowerInvariant()) {
        throw "SHA-256 пакета не совпал: ожидался $Sha256, получен $actual"
    }
    $unpack = Join-Path $work 'package'
    Expand-Archive -LiteralPath $archive -DestinationPath $unpack -Force
    $roots = @(Get-ChildItem -LiteralPath $unpack -Directory | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.txt')
    })
    if ($roots.Count -ne 1) { throw 'В ZIP должен быть ровно один корень runtime-пакета с manifest.txt.' }
    $installer = Join-Path $roots[0].FullName 'install.ps1'
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        throw 'В runtime-пакете отсутствует install.ps1.'
    }
    if ([string]::IsNullOrWhiteSpace($PortalUrl)) {
        $PortalUrl = $PackageUrl.GetLeftPart([UriPartial]::Authority)
    }
    Write-Host '[2/3] Проверка и установка платформы...' -ForegroundColor Cyan
    $arguments = @{ InstallDir = $InstallDir }
    if ($LicenseKey) { $arguments.LicenseKey = $LicenseKey }
    if ($PortalUrl) { $arguments.PortalUrl = $PortalUrl }
    if ($Force) { $arguments.Force = $true }
    if ($NoStart) { $arguments.NoStart = $true }
    & $installer @arguments
    Write-Host '[3/3] Готово. Запустите FloVMP-Server.exe в папке установки.' -ForegroundColor Green
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
}
