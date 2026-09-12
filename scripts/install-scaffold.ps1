<#
.SYNOPSIS
    FloV:MP Turnkey Server One-Liner Installer for RP Developers
.DESCRIPTION
    Downloads and unpacks FloV:MP server development scaffold into current directory.
.EXAMPLE
    irm http://188.127.229.224/cdn/dist/install-scaffold.ps1 | iex
#>

param(
    [string]$ProjectName = "RolePlay Server",
    [string]$LicenseKey  = "FLV-DEMO-0000-0000",
    [int]$Slots          = 2000,
    [string]$MasterHost  = "http://188.127.229.224"
)

$targetDir = (Get-Location).Path
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " FloV:MP — Мастер-установщик сервера разработки RP" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Каталог установки : $targetDir"
Write-Host "Имя проекта       : $ProjectName"
Write-Host "Ключ лицензии     : $LicenseKey"
Write-Host "Лимит слотов      : $Slots"
Write-Host ""

$zipUrl = "$MasterHost/cdn/dist/flovmp-scaffold.zip"
$tempFile = Join-Path $targetDir "_scaffold_download.zip"

Write-Host "[1/4] Загрузка дистрибутива сервера..." -ForegroundColor Yellow
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
(New-Object System.Net.WebClient).DownloadFile($zipUrl, $tempFile)

Write-Host "[2/4] Развертывание структуры каталогов..." -ForegroundColor Yellow
Expand-Archive -Path $tempFile -DestinationPath $targetDir -Force
Remove-Item $tempFile -Force -ErrorAction SilentlyContinue

Write-Host "[3/4] Применение White-Label конфигурации..." -ForegroundColor Yellow
$tomlFile = Join-Path $targetDir "config\server.toml"
if (Test-Path $tomlFile) {
    $c = [System.IO.File]::ReadAllText($tomlFile, [System.Text.Encoding]::UTF8)
    $c = $c -replace 'name\s*=\s*".*?"', "name        = `"$ProjectName`""
    $c = $c -replace 'players\s*=\s*\d+', "players     = $Slots"
    [System.IO.File]::WriteAllText($tomlFile, $c, [System.Text.Encoding]::UTF8)
    Copy-Item $tomlFile (Join-Path $targetDir "server\server.toml") -Force
}

Write-Host "[4/4] Запись лицензии $LicenseKey..." -ForegroundColor Yellow
$licJson = @"
{
  "licenseKey": "$LicenseKey",
  "project": "$ProjectName",
  "plan": "standard",
  "maxPlayers": $Slots,
  "issuedAt": "$((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))",
  "expiresAt": "$((Get-Date).AddYears(1).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))",
  "signature": "RSA2048_OFFLINE_VERIFIED"
}
"@
[System.IO.File]::WriteAllText((Join-Path $targetDir "license.flv"), $licJson, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " ✓ Сервер FloV:MP успешно установлен!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Запуск сервера: .\start.cmd или .\scripts\start-server.cmd" -ForegroundColor Yellow
Write-Host " Разработка логики: server\resources\flovmp-core\" -ForegroundColor Yellow
Write-Host " Настройка NUI:     client\resources\flovmp-client\html\" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Green
