@echo off
setlocal
title FloV:MP Direct Connect

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=127.0.0.1:7788"

echo ========================================================
echo  FloV:MP Direct Connect - Local Development
echo ========================================================
echo  Target Server: %TARGET%
echo ========================================================

rem 0. Check bundled connector inside tools/connector
if exist "%~dp0tools\connector\FloVMP.Connect.exe" (
    echo [INFO] Launching via bundled FloV:MP Connector...
    "%~dp0tools\connector\FloVMP.Connect.exe" -connect "%TARGET%"
    exit /b %errorlevel%
)

rem 1. Check relative repository scripts
if exist "%~dp0..\..\scripts\connect.cmd" (
    call "%~dp0..\..\scripts\connect.cmd" %TARGET%
    exit /b %errorlevel%
)

rem 2. Check root FloV-MP scripts
if exist "C:\FloV-MP\scripts\connect.cmd" (
    call "C:\FloV-MP\scripts\connect.cmd" %TARGET%
    exit /b %errorlevel%
)

rem 3. Check AppData FloVMP Connector
set "APPDATA_CONNECTOR=%LOCALAPPDATA%\FloVMP\engine\FloVMP.Connect.exe"
if exist "%APPDATA_CONNECTOR%" (
    echo [INFO] Launching via FloV:MP Engine Connector...
    "%APPDATA_CONNECTOR%" -connect "%TARGET%"
    exit /b %errorlevel%
)

rem 4. Check AppData client runtime
set "CLIENT_EXE=%LOCALAPPDATA%\FloVMP\runtime\client\flovmp.exe"
if not exist "%CLIENT_EXE%" set "CLIENT_EXE=%LOCALAPPDATA%\FloVMP\runtime\client\altv.exe"
if exist "%CLIENT_EXE%" (
    echo [INFO] Launching game via installed FloV:MP client...
    start "" "%CLIENT_EXE%" -connecturl "altv://connect/%TARGET%" -noupdate
    exit /b 0
)

echo [ERROR] No FloV:MP client or connector found.
echo Please run FloV:MP Launcher or install the client package.
echo.
pause
