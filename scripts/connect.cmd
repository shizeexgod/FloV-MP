@echo off
title FloV:MP Direct Connect

rem ASCII-only on purpose: Cyrillic in a .cmd is parsed in the OEM codepage
rem and breaks line tokenization. Keep this file ASCII.

rem The connector requires administrator rights (manifest requireAdministrator)
rem because it adds a direct route to bypass the VPN. Auto-elevate so the user
rem does not have to remember "Run as administrator".
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Requesting administrator rights ...
    if "%~1"=="" (
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    ) else (
        powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%*' -Verb RunAs"
    )
    exit /b
)

setlocal
cd /d "%~dp0.."

rem The connector is a WinExe (no console): its output goes to the log file
rem %LOCALAPPDATA%\FloridaV\connect.log, so this window stays quiet - that is
rem normal, watch the log for progress. bin\Release is built from current
rem source (has the CDN de-race fix); native-dist is a fallback.
set "CONNECT_EXE=launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe"
rem Всегда пересобираем из актуального исходника (быстрый инкрементальный
rem билд). Если старый коннектор ещё висит и держит DLL - билд не пройдёт,
rem поэтому сначала закройте прошлое окно коннектора и GTA.
echo [INFO] Building FloVMP.Connect (latest source) ...
dotnet build "launcher\src\FloVMP.Connect\FloVMP.Connect.csproj" -c Release --nologo -v q
if not exist "%CONNECT_EXE%" set "CONNECT_EXE=launcher\electron\native-dist\FloVMP.Connect.exe"

if not exist "%CONNECT_EXE%" (
    echo [ERROR] FloVMP.Connect.exe not found. Aborting.
    pause
    exit /b 1
)

echo [INFO] Connector: %CONNECT_EXE%
echo [INFO] Log: %LOCALAPPDATA%\FloridaV\connect.log

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=188.127.229.224:7788"

rem Direct VPN-bypass route is added by the connector itself (it auto-detects
rem the physical IPv4 gateway). No hardcoded gateway here.

echo [INFO] Target server: %TARGET%
echo [INFO] Launching game via connector ...
"%CONNECT_EXE%" -connect "%TARGET%" --client "%~dp0..\runtime\client"

echo.
echo [INFO] Connector exited. Press any key to close.
pause >nul
endlocal
