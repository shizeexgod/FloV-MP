@echo off
chcp 65001 >nul
title FloV:MP Server
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-server.ps1"
if errorlevel 1 (
    echo.
    echo [FloV:MP] Сервер завершился с ошибкой. Лог: server\server.log
    pause
)
