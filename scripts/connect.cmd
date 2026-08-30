@echo off
set "HERE=%~dp0"
set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=127.0.0.1:7788"

echo [FloV:MP] Подключение к %TARGET%
echo [FloV:MP] Автоопределение GTA V (Legacy / Enhanced из Epic Games)...
echo.

set "CONNECT_DLL=%HERE%..\launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.dll"

if exist "%CONNECT_DLL%" (
  dotnet "%CONNECT_DLL%" -connect %TARGET% %2 %3 %4 %5 %6 %7 %8 %9
) else (
  dotnet run --project "%HERE%..\launcher\src\FloVMP.Connect\FloVMP.Connect.csproj" -c Release -- -connect %TARGET% %2 %3 %4 %5 %6 %7 %8 %9
)

echo.
pause
