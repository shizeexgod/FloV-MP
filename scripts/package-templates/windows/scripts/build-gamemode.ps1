# Сборка вашего сервера (папка gamemode) в server\resources\gamemode.
# Запускается из gamemode\build.cmd.
. (Join-Path $PSScriptRoot "lib.ps1")

$gamemode = Join-Path $Script:Root "gamemode"
$project  = Join-Path $gamemode "Gamemode.csproj"
$template = Join-Path $Script:Root "sdk\template"

if (-not (Test-Path $project)) {
    if (-not (Test-Path $template)) { throw "Нет папки gamemode и шаблона sdk\template — распакуйте архив полностью." }
    Copy-Item $template $gamemode -Recurse
    Write-Host "[FloV:MP] Папка gamemode создана из шаблона." -ForegroundColor Green
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$sdkOk = $false
if ($dotnet) {
    foreach ($line in (& dotnet --list-sdks 2>$null)) {
        if ($line -match '^(\d+)\.' -and [int]$Matches[1] -ge 8) { $sdkOk = $true }
    }
}
if (-not $sdkOk) {
    Write-Host "[FloV:MP] Для сборки нужен .NET SDK 8 (или новее), а не только Runtime:" -ForegroundColor Red
    Write-Host "          https://dotnet.microsoft.com/download/dotnet/8.0 — раздел SDK, x64." -ForegroundColor Red
    exit 1
}

Write-Host "[FloV:MP] Сборка gamemode..." -ForegroundColor Cyan
& dotnet build $project -c Release -nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FloV:MP] Сборка не удалась — ошибки выше." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Подключаем ресурс в server.toml (если сервер уже запускался и файл создан).
$toml = Join-Path $Script:Root "server\server.toml"
if (Test-Path $toml) {
    $text = [System.IO.File]::ReadAllText($toml)
    $m = [regex]::Match($text, '(?ms)^resources\s*=\s*\[(.*?)^\s*\]')
    if ($m.Success -and -not $m.Groups[1].Value.Contains('"gamemode"')) {
        $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $at = $m.Groups[1].Index + $m.Groups[1].Length
        $text = $text.Insert($at, '    "gamemode",' + $nl)
        [System.IO.File]::WriteAllText($toml, $text, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "[FloV:MP] Ресурс gamemode подключён в server\server.toml." -ForegroundColor Green
    }
}

Write-Host "[FloV:MP] Готово: server\resources\gamemode. Перезапустите сервер (FloVMP-Server.exe)." -ForegroundColor Green
