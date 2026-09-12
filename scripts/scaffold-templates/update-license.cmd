@echo off
chcp 65001 >nul
title Проверка лицензии FloV:MP

if exist "%~dp0..\license.flv" (
    echo [ИНФО] Файл лицензии license.flv присутствует:
    type "%~dp0..\license.flv"
) else (
    echo [ВНИМАНИЕ] Файл license.flv не найден.
    echo Поместите файл лицензии в корень каталога проекта.
)
echo.
pause
