@echo off
rem Подключение к серверу FloV:MP:  play.cmd [адрес сервера] [ник]
rem Без адреса берётся server.txt рядом.
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play.ps1" -Address "%~1" -Name "%~2"
if errorlevel 1 pause
