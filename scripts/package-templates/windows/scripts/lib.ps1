# Общие функции скриптов FloV:MP для Windows.

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$Script:Root = Split-Path -Parent $PSScriptRoot

function Import-FlovmpEnv {
    # Читаем config\flovmp.env как пары КЛЮЧ=значение, не исполняя их.
    param([string]$Path = (Join-Path $Script:Root "config\flovmp.env"))
    if (-not (Test-Path $Path)) { return }
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
            $key = $Matches[1]; $val = $Matches[2]
            if ($val -match '^"(.*)"$' -or $val -match "^'(.*)'$") { $val = $Matches[1] }
            [Environment]::SetEnvironmentVariable($key, $val, "Process")
        }
    }
}

function New-RandomHex([int]$Bytes) {
    $buf = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buf)
    return -join ($buf | ForEach-Object { $_.ToString("x2") })
}

function Set-EnvValue([string]$Path, [string]$Key, [string]$Value) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $found = $false
    if (Test-Path $Path) {
        foreach ($l in [System.IO.File]::ReadAllLines($Path)) {
            if ($l -match "^$Key=") { $lines.Add("$Key=$Value"); $found = $true } else { $lines.Add($l) }
        }
    }
    if (-not $found) { $lines.Add("$Key=$Value") }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($Path, $lines, $utf8)
}

function Initialize-FlovmpConfig {
    # Первичная настройка: создаёт ваши файлы, если их ещё нет. Существующие
    # не трогает никогда.
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $envPath = Join-Path $Script:Root "config\flovmp.env"
    if (-not (Test-Path $envPath)) {
        Copy-Item (Join-Path $Script:Root "config\flovmp.env.example") $envPath
        $hex = (New-RandomHex 8).ToUpper()
        $token = "FLV-" + ($hex.Substring(0,4), $hex.Substring(4,4), $hex.Substring(8,4), $hex.Substring(12,4) -join "-")
        Set-EnvValue $envPath "FLOVMP_SETUP_TOKEN" $token
        Write-Host "[FloV:MP] Создан config\flovmp.env" -ForegroundColor Green
    }

    $serverToml = Join-Path $Script:Root "server\server.toml"
    $voiceToml  = Join-Path $Script:Root "voice\voice.toml"
    if (-not (Test-Path $serverToml) -or -not (Test-Path $voiceToml)) {
        $buf = New-Object byte[] 4
        [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buf)
        $secret = ([BitConverter]::ToUInt32($buf, 0) % 2147483646) + 1
        $map = @{
            "__FLOVMP_NAME__"              = "FloV:MP Dev Server"
            "__FLOVMP_PORT__"              = "7788"
            "__FLOVMP_PLAYERS__"           = "100"
            "__FLOVMP_VOICE_SECRET__"      = "$secret"
            "__FLOVMP_VOICE_PORT__"        = "7896"
            "__FLOVMP_VOICE_PUBLIC_HOST__" = "127.0.0.1"
            "__FLOVMP_VOICE_PUBLIC_PORT__" = "7895"
        }
        foreach ($pair in @(@("server\server.toml.example", $serverToml), @("voice\voice.toml.example", $voiceToml))) {
            $text = [System.IO.File]::ReadAllText((Join-Path $Script:Root $pair[0]), $utf8)
            foreach ($k in $map.Keys) { $text = $text.Replace($k, $map[$k]) }
            [System.IO.File]::WriteAllText($pair[1], $text, $utf8)
        }
        Write-Host "[FloV:MP] Созданы server\server.toml и voice\voice.toml (голос: 127.0.0.1:7895)" -ForegroundColor Green
        Write-Host "[FloV:MP] Для игроков из интернета укажите внешний IP в externalPublicHost (server\server.toml)." -ForegroundColor Yellow
    }
}
