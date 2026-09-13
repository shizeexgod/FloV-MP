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

if exist "C:\FloV-MP\scripts\connect.cmd" (
    call "C:\FloV-MP\scripts\connect.cmd" %TARGET%
    exit /b %errorlevel%
)

echo [ERROR] Main connector C:\FloV-MP\scripts\connect.cmd not found.
echo Please run FloV:MP Launcher or connect via client connector.
echo.
pause
