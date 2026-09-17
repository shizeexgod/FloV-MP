# FloV:MP — запуск сервера на Windows (вызывается из start.cmd).
. (Join-Path $PSScriptRoot "lib.ps1")

Initialize-FlovmpConfig
Import-FlovmpEnv

$serverExe = Join-Path $Script:Root "server\flovmp-server.exe"
$voiceExe  = Join-Path $Script:Root "voice\altv-voice-server.exe"
if (-not (Test-Path $serverExe)) { throw "Не найден server\flovmp-server.exe — распакуйте архив полностью." }

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$hasRuntime = $false
if ($dotnet) { $hasRuntime = [bool](& dotnet --list-runtimes 2>$null | Select-String "Microsoft.NETCore.App 8\.") }
if (-not $hasRuntime) {
    Write-Host "[FloV:MP] Не найден .NET 8 Runtime. Скачайте «.NET Runtime 8» (x64): https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

if ((Test-Path $voiceExe) -and -not (Get-Process altv-voice-server -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $voiceExe -WorkingDirectory (Join-Path $Script:Root "voice") -WindowStyle Minimized
    Write-Host "[FloV:MP] Голосовой сервер запущен (свёрнутое окно)." -ForegroundColor Green
}

$token = [Environment]::GetEnvironmentVariable("FLOVMP_SETUP_TOKEN")
if ($token) { Write-Host "[FloV:MP] Стать владельцем: в игре /claimowner $token" -ForegroundColor Cyan }

Set-Location (Join-Path $Script:Root "server")
& $serverExe
exit $LASTEXITCODE
