@echo off
chcp 65001 > nul
title FloV:MP Direct Connect
echo ===================================================
echo   Starting FloV:MP (Headless Connector)
echo ===================================================

cd /d "%~dp0.."

if not exist "launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe" (
    echo [INFO] Building FloVMP.Connect...
    dotnet build launcher\src\FloVMP.Connect\FloVMP.Connect.csproj -c Release --nologo > nul
)

set TARGET=%1
if "%TARGET%"=="" set TARGET=188.127.229.224:7788

REM Прямой маршрут в обход VPN коннектор добавляет сам (авто-определение
REM физического IPv4-шлюза внутри FloVMP.Connect). Хардкод шлюза убран —
REM он ломался на сетях, где шлюз не 192.168.0.1.

echo [INFO] Target server: %TARGET%
echo [INFO] Launching game...
launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe -connect %TARGET% --client "%~dp0..\runtime\client"

pause
