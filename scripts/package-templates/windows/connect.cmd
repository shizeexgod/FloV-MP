@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
title FloV:MP

rem Primary entry point for the bundled Legacy b3889 client:
rem connect.cmd [host:port]
rem Without an argument play.ps1 reads client-b3889\server.txt, written by the
rem installer with this customer's public VDS address and game port.
rem NOTE: keep this file pure ASCII. cmd.exe parses the whole IF (...) block
rem before chcp takes effect, and Cyrillic inside it breaks the parser.
set "TARGET=%~1"
:run
if "%TARGET%"=="" (
    echo [FloV:MP] Подключение к серверу из client-b3889\server.txt
) else (
    echo [FloV:MP] Подключение к %TARGET%
)

rem The b3889 client is the normal product path.
if exist "%~dp0client-b3889\play.cmd" (
    if "%TARGET%"=="" (
        call "%~dp0client-b3889\play.cmd"
    ) else (
        call "%~dp0client-b3889\play.cmd" "%TARGET%" "%~2"
    )
    exit /b %errorlevel%
)

if "%TARGET%"=="" (
    echo [FloV:MP] client-b3889 missing; pass customer host:port explicitly.
    exit /b 2
)

rem Compatibility fallback for old packages without client-b3889.
if exist "%~dp0tools\connector\FloVMP.Connect.exe" (
    "%~dp0tools\connector\FloVMP.Connect.exe" -connect "%TARGET%"
    exit /b %errorlevel%
)

set "APPDATA_CONNECTOR=%LOCALAPPDATA%\FloVMP\engine\FloVMP.Connect.exe"
if exist "%APPDATA_CONNECTOR%" (
    "%APPDATA_CONNECTOR%" -connect "%TARGET%"
    exit /b %errorlevel%
)

echo [FloV:MP] Клиентский connector не входит в серверный пакет.
echo [FloV:MP] Установите и настройте собственный совместимый клиент/launcher.
echo [FloV:MP] Необязательный путь connector: %LOCALAPPDATA%\FloVMP\engine\FloVMP.Connect.exe
pause
exit /b 1
