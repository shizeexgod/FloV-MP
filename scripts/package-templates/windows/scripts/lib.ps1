# Общие функции скриптов FloV:MP для Windows (резервное копирование базы).
# Сервер запускается FloVMP-Server.exe — он же создаёт настройки при первом запуске.

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
