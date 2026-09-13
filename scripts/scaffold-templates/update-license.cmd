@echo off
setlocal
title FloV:MP License Inspector

if exist "%~dp0..\license.flv" (
    echo ========================================================
    echo  FloV:MP License File (license.flv)
    echo ========================================================
    type "%~dp0..\license.flv"
    echo.
    echo ========================================================
) else (
    echo [WARNING] license.flv not found in project root.
    echo Place your FloV:MP license key in the root directory.
)
echo.
pause
