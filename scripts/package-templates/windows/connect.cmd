@echo off
setlocal
title FloV:MP Direct Connect

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=127.0.0.1:7788"

echo ========================================================
echo  FloV:MP Direct Connect: %TARGET%
echo ========================================================

if exist "%~dp0tools\connector\FloVMP.Connect.exe" (
    "%~dp0tools\connector\FloVMP.Connect.exe" -connect "%TARGET%"
    exit /b %errorlevel%
)

set "APPDATA_CONNECTOR=%LOCALAPPDATA%\FloVMP\engine\FloVMP.Connect.exe"
if exist "%APPDATA_CONNECTOR%" (
    "%APPDATA_CONNECTOR%" -connect "%TARGET%"
    exit /b %errorlevel%
)

echo [ERROR] FloV:MP connector not found. Install the FloV:MP Launcher first.
pause
exit /b 1
