@echo off
setlocal
title FloV:MP Legacy 3889 bridge

set "ROOT=%~dp0.."
set "CONNECT=%ROOT%\launcher\src\FloVMP.Connect\bin\Debug\net8.0-windows\FloVMP.Connect.exe"
set "GTA=%ProgramFiles%\9d2d0eb64d5c44529cece33fe2a46482"

if not exist "%CONNECT%" (
  echo [ERROR] Connector is not built. Run: dotnet build launcher\src\FloVMP.Connect\FloVMP.Connect.csproj
  exit /b 2
)
if not exist "%GTA%\GTA5.exe" (
  echo [ERROR] GTA Legacy b3889 was not found at:
  echo         %GTA%
  echo Edit GTA in this file or pass the executable manually through the connector.
  exit /b 2
)

echo [INFO] This is the native b3889 bootstrap path, not the old alt:V client path.
echo [INFO] Server native bridge endpoint must listen on TCP 7798.
"%CONNECT%" --bridge-3889 -connect 127.0.0.1:7788 --gta "%GTA%" --client "%ROOT%\runtime\client" --keep-open
set "CODE=%ERRORLEVEL%"
echo.
echo [INFO] Connector exit code: %CODE%
pause
exit /b %CODE%
