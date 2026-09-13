@echo off
setlocal enabledelayedexpansion
title FloV:MP Dedicated Server (RolePlay Engine)
cd /d "%~dp0.."

echo ========================================================
echo  FloV:MP Dedicated Server - RolePlay Engine
echo ========================================================
echo  Server Root: %CD%
echo ========================================================

if not exist "server\flovmp-server.exe" (
    echo [ERROR] server\flovmp-server.exe not found!
    echo Please make sure the server package is properly installed.
    echo.
    pause
    exit /b 1
)

if not exist "license.flv" (
    echo [NOTICE] license.flv not found in project root.
    echo Starting server in evaluation mode.
    echo.
)

if exist "config\flovmp.env" (
    echo [INFO] Loading environment variables from config\flovmp.env ...
    for /f "usebackq tokens=1* delims==" %%A in ("config\flovmp.env") do (
        set "KEY=%%A"
        set "VAL=%%B"
        if defined KEY if not "!KEY:~0,1!"=="#" set "!KEY!=!VAL!"
    )
)

echo [INFO] Starting FloV:MP Server process...
echo.
cd /d "%~dp0..\server"
flovmp-server.exe

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ========================================================
    echo [WARNING] Server terminated with exit code: %ERRORLEVEL%
    echo ========================================================
    echo.
    pause
)
