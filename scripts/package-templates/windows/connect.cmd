@echo off
chcp 65001 >nul
setlocal
title FloV:MP — подключение к серверу

rem Подключение к серверу с этого ПК: connect.cmd [адрес:порт]
rem По умолчанию — сервер, запущенный здесь же (127.0.0.1:7788).
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=127.0.0.1:7788"

echo [FloV:MP] Подключение к %TARGET%

if exist "%~dp0tools\connector\FloVMP.Connect.exe" (
    "%~dp0tools\connector\FloVMP.Connect.exe" -connect "%TARGET%"
    exit /b %errorlevel%
)

set "APPDATA_CONNECTOR=%LOCALAPPDATA%\FloVMP\engine\FloVMP.Connect.exe"
if exist "%APPDATA_CONNECTOR%" (
    "%APPDATA_CONNECTOR%" -connect "%TARGET%"
    exit /b %errorlevel%
)

echo [FloV:MP] Не найден клиент FloV:MP. Установите лаунчер FloV:MP и подключитесь через него.
pause
exit /b 1
