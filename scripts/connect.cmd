@echo off
title FloV:MP Direct Connect
echo ===================================================
echo   Запуск FloV:MP (Headless Connector)
echo ===================================================

cd /d "%~dp0.."

:: Проверяем, собран ли коннектор
if not exist "launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe" (
    echo [INFO] Сборка FloVMP.Connect...
    dotnet build launcher\src\FloVMP.Connect\FloVMP.Connect.csproj -c Release --nologo > nul
)

:: Запускаем коннектор напрямую на локальный сервер
:: (Путь к GTA он найдет сам через реестр, или можно передать --gta "C:\Path")
echo [INFO] Запуск игры...
launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe --host 127.0.0.1 --port 7788 --nick "Admin"

pause
