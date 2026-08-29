@echo off
REM Подключение к серверу FloV:MP через свой коннектор.
REM Использование:  connect.cmd [ip:port]   (по умолчанию 127.0.0.1:7788)
setlocal
set TARGET=%~1
if "%TARGET%"=="" set TARGET=127.0.0.1:7788

set CONNECT=%~dp0\..\launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe
if not exist "%CONNECT%" (
  echo [FloV:MP] коннектор не собран. Собери:
  echo   dotnet build "%~dp0\..\launcher\src\FloVMP.Connect\FloVMP.Connect.csproj" -c Release
  pause
  exit /b 1
)

echo [FloV:MP] connect -> %TARGET%
echo [FloV:MP] ВАЖНО: GTA V из Epic -> Epic Games Launcher должен быть ЗАПУЩЕН и залогинен.
echo.
"%CONNECT%" -connect %TARGET%
pause
