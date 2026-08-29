@echo off
REM Запуск сервера FloV:MP без возни с execution policy.
cd /d "%~dp0\..\runtime\server"
if not exist altv-server.exe (
  echo [FloV:MP] runtime\server не собран. Запусти: scripts\assemble-runtime.cmd
  pause
  exit /b 1
)
echo [FloV:MP] сервер :7788  (Ctrl+C для остановки)
altv-server.exe
