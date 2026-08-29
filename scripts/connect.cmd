@echo off
REM Подключение к серверу FloV:MP через свой коннектор.
REM   connect.cmd                         -> 127.0.0.1:7788, автодетект игры
REM   connect.cmd 1.2.3.4:7788            -> другой сервер
REM   connect.cmd 127.0.0.1:7788 --gta "E:\...\GTAV Enhanced"
REM Любые доп. аргументы после ip:port пробрасываются в коннектор
REM (--gta <dir>, --client <dir>, --no-directlaunch, --keep-open).
setlocal
set TARGET=%~1
if "%TARGET%"=="" set TARGET=127.0.0.1:7788
shift

set CONNECT=%~dp0\..\launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe
if not exist "%CONNECT%" (
  echo [FloV:MP] коннектор не собран. Собери:
  echo   dotnet build "%~dp0\..\launcher\src\FloVMP.Connect\FloVMP.Connect.csproj" -c Release
  pause
  exit /b 1
)

REM собрать хвост аргументов (%2 %3 ... после сдвига это %1 %2 ...)
set REST=
:loop
if "%~1"=="" goto run
set REST=%REST% %1
shift
goto loop

:run
echo [FloV:MP] connect -> %TARGET%   %REST%
echo [FloV:MP] GTA V из Epic -> Epic Games Launcher должен быть ЗАПУЩЕН и залогинен.
echo.
"%CONNECT%" -connect %TARGET%%REST%
pause
