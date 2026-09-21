@echo off
rem Установка клиента FloV:MP в GTA V Legacy 1.0.3889.0 (нужны права на запись в папку игры).
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-client.ps1" %*
echo.
pause
