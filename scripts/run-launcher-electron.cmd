@echo off
title Derzhava RP Launcher (Electron)
cd /d "%~dp0.."

if not exist "launcher\src\FloVMP.Launcher.Native\bin\Release\net8.0-windows\FloVMP.Launcher.Native.exe" (
    echo Building FloVMP.Launcher.Native...
    dotnet build launcher\src\FloVMP.Launcher.Native\FloVMP.Launcher.Native.csproj -c Release
)

if not exist "launcher\electron\node_modules" (
    echo Installing Electron dependencies, this only happens once...
    pushd launcher\electron
    call npm install
    popd
)

pushd launcher\electron
call npm start
popd
