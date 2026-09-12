@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
title Резервное копирование базы данных RolePlay

set BACKUP_DIR=%~dp0..\backups
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set dt=%%I
set TIMESTAMP=%dt:~0,4%-%dt:~4,2%-%dt:~6,2%_%dt:~8,2%-%dt:~10,2%-%dt:~12,2%
set DUMP_FILE=%BACKUP_DIR%\db_backup_%TIMESTAMP%.sql

echo Создание резервной копии базы данных...
mysqldump -u root --databases flovmp_rp > "%DUMP_FILE%"
if %ERRORLEVEL% EQU 0 (
    echo [УСПЕХ] Резервная копия сохранена в: %DUMP_FILE%
) else (
    echo [ОШИБКА] Не удалось выполнить mysqldump. Проверьте установку MySQL/MariaDB.
)
pause
