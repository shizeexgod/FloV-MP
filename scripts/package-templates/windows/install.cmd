@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
if errorlevel 1 (
  echo.
  echo Установка не выполнена. Исходная папка не изменена до проверки манифеста.
  exit /b 1
)
