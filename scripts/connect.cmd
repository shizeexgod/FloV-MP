@echo off
set "HERE=%~dp0"
set "CONNECT=%HERE%..\launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe"

if not exist "%CONNECT%" (
  echo [FloV:MP] connector not built. Run:
  echo   dotnet build "%HERE%..\launcher\src\FloVMP.Connect\FloVMP.Connect.csproj" -c Release
  pause
  exit /b 1
)

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=127.0.0.1:7788"

echo [FloV:MP] connect to %TARGET%   %2 %3 %4 %5 %6
echo [FloV:MP] Epic Games Launcher must be RUNNING and logged in.
echo.
"%CONNECT%" -connect %TARGET% %2 %3 %4 %5 %6 %7 %8 %9
echo.
pause
