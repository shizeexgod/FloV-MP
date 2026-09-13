@echo off
setlocal enabledelayedexpansion
title FloV:MP Database Backup

set "BACKUP_DIR=%~dp0..\backups"
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value 2^>nul') do set "dt=%%I"
if not defined dt (
    set "TIMESTAMP=%date%_%time%"
) else (
    set "TIMESTAMP=!dt:~0,4!-!dt:~4,2!-!dt:~6,2!_!dt:~8,2!-!dt:~10,2!-!dt:~12,2!"
)
set "DUMP_FILE=%BACKUP_DIR%\db_backup_%TIMESTAMP%.sql"

echo [INFO] Creating MariaDB / MySQL database backup...
mysqldump -u root --databases flovmp_rp > "%DUMP_FILE%" 2>nul
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Backup saved to: %DUMP_FILE%
) else (
    echo [WARNING] mysqldump command failed or MariaDB not running.
    echo Please verify that MySQL/MariaDB service is installed and accessible.
)
echo.
pause
