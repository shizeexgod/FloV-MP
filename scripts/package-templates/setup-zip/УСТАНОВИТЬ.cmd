@echo off
chcp 65001 >nul
title FloV:MP - установка и обновление
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0flovmp-setup.ps1" %*
set RC=%ERRORLEVEL%
echo.
if not "%RC%"=="0" (
  echo Установка не выполнена. Папка сервера не изменена.
) else (
  echo Готово.
)
echo.
pause
exit /b %RC%
