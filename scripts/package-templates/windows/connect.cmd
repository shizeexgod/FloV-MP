@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
title FloV:MP

rem Primary entry point for the bundled Legacy b3889 client:
rem connect.cmd [host:port]
rem Without an argument the port is read from server\server.toml: the owner may
rem change it, and a hardcoded 7788 would point at a server that is not there.
rem NOTE: keep this file pure ASCII. cmd.exe parses the whole IF (...) block
rem before chcp takes effect, and Cyrillic inside it breaks the parser.
set "TARGET=%~1"
if not "%TARGET%"=="" goto run

set "PORT="
if exist "%~dp0server\server.toml" (
    for /f "tokens=2 delims== " %%p in ('findstr /r /c:"^port[ ]*=" "%~dp0server\server.toml"') do (
        if not defined PORT set "PORT=%%p"
    )
)
if not defined PORT set "PORT=7788"
set "TARGET=127.0.0.1:!PORT!"

:run
echo [FloV:MP] Подключение к %TARGET%

rem The b3889 client is the normal product path.
if exist "%~dp0client-b3889\play.cmd" (
    call "%~dp0client-b3889\play.cmd" "%TARGET%" "%~2"
    exit /b %errorlevel%
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
