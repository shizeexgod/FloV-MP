@echo off
rem Подключение к серверу FloV:MP:  play.cmd 1.2.3.4:7788 [Ник]
chcp 65001 >nul
if "%~1"=="" (
  echo Использование: play.cmd ^<адрес сервера^> [ник]
  echo Пример:        play.cmd 1.2.3.4:7788 Shize
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1" -Address "%~1" -Name "%~2"
