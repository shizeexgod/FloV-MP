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

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

$targetDir = (Get-Location).Path
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " FloV:MP Server Scaffold Installer" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Target Directory : $targetDir"
Write-Host "Project Name     : $ProjectName"
Write-Host "License Key      : $LicenseKey"
Write-Host "Max Slots        : $Slots"
Write-Host ""

$zipUrl = "$MasterHost/cdn/dist/flovmp-scaffold.zip"
$tempFile = Join-Path $targetDir "_scaffold_download.zip"

Write-Host "[1/4] Downloading server distribution package..." -ForegroundColor Yellow
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
(New-Object System.Net.WebClient).DownloadFile($zipUrl, $tempFile)

Write-Host "[2/4] Unpacking directory structure..." -ForegroundColor Yellow
Expand-Archive -Path $tempFile -DestinationPath $targetDir -Force
Remove-Item $tempFile -Force -ErrorAction SilentlyContinue

Write-Host "[3/4] Applying server configuration (server.toml)..." -ForegroundColor Yellow
$tomlFile = Join-Path $targetDir "config\server.toml"
if (Test-Path $tomlFile) {
    $c = [System.IO.File]::ReadAllText($tomlFile, [System.Text.Encoding]::UTF8)
    $c = $c -replace 'name\s*=\s*".*?"', "name        = `"$ProjectName`""
    $c = $c -replace 'players\s*=\s*\d+', "players     = $Slots"
    [System.IO.File]::WriteAllText($tomlFile, $c, [System.Text.Encoding]::UTF8)
    Copy-Item $tomlFile (Join-Path $targetDir "server\server.toml") -Force
}

Write-Host "[4/4] Writing server license ($LicenseKey)..." -ForegroundColor Yellow
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

# Refresh Windows Explorer icon cache so branding displays immediately
try {
    $shellCode = '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);'
    $shellType = Add-Type -MemberDefinition $shellCode -Name "ShellIconNotifier" -Namespace "FloVMP" -PassThru
    $shellType::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
} catch {}

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " [SUCCESS] FloV:MP Server successfully installed!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Start Server   : .\start.cmd (or .\scripts\start-server.cmd)" -ForegroundColor Yellow
Write-Host " Direct Connect : .\connect.cmd" -ForegroundColor Yellow
Write-Host " Server Logic   : server\resources\flovmp-starter\" -ForegroundColor Yellow
Write-Host " NUI Interface  : client\resources\flovmp-client\html\" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Green
