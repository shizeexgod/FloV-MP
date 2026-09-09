@echo off
title FloV:MP Launcher (Держава Онлайн)
cd /d "%~dp0.."

if not exist "launcher\src\FloVMP.Launcher.Native\bin\Release\net8.0-windows\FloVMP.Launcher.Native.exe" (
    echo [FloV:MP] Building FloVMP.Launcher.Native helper...
    dotnet build launcher\src\FloVMP.Launcher.Native\FloVMP.Launcher.Native.csproj -c Release
)

if not exist "launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe" (
    echo [FloV:MP] Building FloVMP.Connect connector...
    dotnet build launcher\src\FloVMP.Connect\FloVMP.Connect.csproj -c Release
)

if not exist "launcher\electron\node_modules" (
    echo [FloV:MP] Installing Electron dependencies...
    pushd launcher\electron
    call npm install
    popd
)

pushd launcher\electron
call npm start
popd
