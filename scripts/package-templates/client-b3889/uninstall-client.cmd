@echo off
rem Удалить клиент FloV:MP и вернуть игру в исходное состояние.
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-client.ps1" -Uninstall %*
echo.
pause
