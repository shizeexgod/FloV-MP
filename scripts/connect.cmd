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

echo [INFO] Launching game...
launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe -connect 127.0.0.1:7788 --client "%~dp0..\runtime\client"

pause
