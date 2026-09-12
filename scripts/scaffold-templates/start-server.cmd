@echo off
chcp 65001 >nul
title RolePlay Server (FloV:MP Runtime)
cd /d "%~dp0.."

echo ===================================================
echo  Запуск игрового сервера RolePlay (FloV:MP Runtime)
echo ===================================================

if not exist "server\altv-server.exe" (
    echo [ОШИБКА] server\altv-server.exe не найден!
    pause
    exit /b 1
)

if not exist "license.flv" (
    echo [ПРЕДУПРЕЖДЕНИЕ] Файл license.flv не найден в корне проекта.
    echo Сервер запустится в ознакомительном режиме.
)

REM Загрузка переменных окружения если есть flovmp.env
if exist "config\flovmp.env" (
    for /f "usebackq tokens=1* delims==" %%A in ("config\flovmp.env") do (
        if not "%%A"=="" if not "%%A:~0,1%"=="#" set "%%A=%%B"
    )
)

cd /d "%~dp0..\server"
altv-server.exe
if %ERRORLEVEL% NEQ 0 (
    echo [ВНИМАНИЕ] Сервер завершил работу с кодом: %ERRORLEVEL%
    pause
)
