# FloV:MP — резервная копия базы данных (Windows). mysqldump ищется в PATH и в папках MariaDB/MySQL.
. (Join-Path $PSScriptRoot "lib.ps1")
Import-FlovmpEnv

$db   = if ($env:FLOVMP_DB_NAME) { $env:FLOVMP_DB_NAME } else { "flovmp_server" }
$user = if ($env:FLOVMP_DB_USER) { $env:FLOVMP_DB_USER } else { "flovmp" }
$dbHost = if ($env:FLOVMP_DB_HOST) { $env:FLOVMP_DB_HOST } else { "127.0.0.1" }
$port = if ($env:FLOVMP_DB_PORT) { $env:FLOVMP_DB_PORT } else { "3306" }
if (-not $env:FLOVMP_DB_PASSWORD) { throw "База не настроена: нет FLOVMP_DB_PASSWORD в config\flovmp.env" }
# Установщик MariaDB для Windows не добавляет bin в PATH — ищем и в стандартных папках.
$mysqldump = (Get-Command mysqldump -ErrorAction SilentlyContinue).Source
if (-not $mysqldump) {
    $mysqldump = Get-ChildItem -Path "$env:ProgramFiles\MariaDB*\bin\mysqldump.exe", "$env:ProgramFiles\MySQL\MySQL Server*\bin\mysqldump.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $mysqldump) { throw "mysqldump не найден — установите MariaDB или добавьте её папку bin в PATH" }

$dir = Join-Path $Script:Root "backups"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$file = Join-Path $dir ("db_{0}_{1}.sql" -f $db, (Get-Date -Format "yyyy-MM-dd_HH-mm-ss"))

# Пароль через окружение, а не аргументом командной строки.
$env:MYSQL_PWD = $env:FLOVMP_DB_PASSWORD
& $mysqldump -h $dbHost -P $port -u $user --single-transaction --routines --triggers "--result-file=$file" $db
if ($LASTEXITCODE -ne 0) { Remove-Item $file -ErrorAction SilentlyContinue; throw "mysqldump завершился с ошибкой $LASTEXITCODE" }
Write-Host "[FloV:MP] Копия сохранена: $file" -ForegroundColor Green

# Храним 14 дней (FLOVMP_BACKUP_KEEP_DAYS), как на Linux: иначе папка копий растёт без конца.
$keepDays = if ($env:FLOVMP_BACKUP_KEEP_DAYS -match '^\d+$') { [int]$env:FLOVMP_BACKUP_KEEP_DAYS } else { 14 }
Get-ChildItem -Path $dir -Filter ("db_{0}_*.sql" -f $db) -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$keepDays) } |
    Remove-Item -ErrorAction SilentlyContinue
