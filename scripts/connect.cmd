@echo off
setlocal
title FloV:MP Direct Connect

rem ASCII-only on purpose: Cyrillic in a .cmd is parsed in the OEM codepage
rem and breaks line tokenization. Keep this file ASCII.

cd /d "%~dp0.."

set "CONNECT_EXE=launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe"

if not exist "%CONNECT_EXE%" (
    echo [INFO] Building FloVMP.Connect ...
    dotnet build "launcher\src\FloVMP.Connect\FloVMP.Connect.csproj" -c Release --nologo
)

if not exist "%CONNECT_EXE%" (
    echo [ERROR] FloVMP.Connect.exe not found after build. Aborting.
    pause
    exit /b 1
)

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
