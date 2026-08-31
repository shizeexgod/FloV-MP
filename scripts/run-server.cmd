@echo off
title FloV:MP Server
cd /d "%~dp0.."
set FLOVMP_SERVER_DIR=%~dp0..\runtime\server
server\src\FloVMP.ServerLauncher\bin\Release\net8.0\FloVMP.ServerLauncher.exe
pause
