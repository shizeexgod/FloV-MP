@echo off
title FloV:MP - Fix client build (16.3.15 -> 16.4.39)
setlocal

rem ============================================================================
rem  ROOT FIX for WRONG_STABLE_BUILD.
rem
rem  Cause: runtime\client was running the WRONG alt:V client build 16.3.15
rem  (altv-client.dll, 52,611,704 bytes). The server (avds-rg1s7j) runs the
rem  official release build 16.4.39, so it kicked the older client as
rem  "Wrong Stable build".
rem
rem  Fix (NO patching): copy the GENUINE official 16.4.39 release client
rem  (altv-client.dll, 30,309,480 bytes, SHA1 287a4443...) into runtime\client
rem  in the three places the connector reads it. Then client build == server
rem  build 16.4.39 and the kick disappears.
rem
rem  ASCII-only on purpose (Cyrillic breaks .cmd tokenization).
rem  NOTE: delayed expansion is intentionally OFF - the source path contains a
rem  '!' (UnigramPreview_g9c9v27vpyspw!App) which delayed expansion would eat.
rem ============================================================================

set "SRC=C:\Users\User\Downloads\38833FF26BA1D.UnigramPreview_g9c9v27vpyspw!App\gtamp\sources\payload\altv-client.dll"
set "RC=C:\FloV-MP\runtime\client"
set "WANT_HASH=287a4443ae6234e27c10c56325886a8adbca9771"

echo [INFO] Source (genuine 16.4.39): %SRC%
if not exist "%SRC%" (
    echo [ERROR] Genuine 16.4.39 client not found at the path above.
    echo         Aborting - nothing changed.
    pause
    exit /b 1
)

rem Verify the source really is the genuine 16.4.39 build before touching anything.
set "SRC_HASH="
for /f "skip=1 tokens=1 delims= " %%H in ('certutil -hashfile "%SRC%" SHA1') do if not defined SRC_HASH set "SRC_HASH=%%H"
rem certutil prints the hash with no spaces; normalize case for compare.
if /I not "%SRC_HASH%"=="%WANT_HASH%" (
    echo [ERROR] Source SHA1 mismatch.
    echo         got:  %SRC_HASH%
    echo         want: %WANT_HASH%
    echo         Aborting - nothing changed.
    pause
    exit /b 1
)
echo [OK] Source verified as genuine 16.4.39 (SHA1 %SRC_HASH%).

rem Close any running client so the dll is not locked.
taskkill /F /IM altv.exe           >nul 2>&1
taskkill /F /IM flovmp.exe         >nul 2>&1
taskkill /F /IM FloVMP.Connect.exe >nul 2>&1
ping -n 2 127.0.0.1 >nul

echo [INFO] Backing up current (16.3.15) dll ...
if not exist "%RC%\backup" mkdir "%RC%\backup" >nul 2>&1
copy /Y "%RC%\altv-client.dll" "%RC%\backup\altv-client.dll.1615.bak" >nul 2>&1

echo [INFO] Installing genuine 16.4.39 client (3 targets) ...
copy /Y "%SRC%" "%RC%\altv-client.dll"          >nul || goto :failcopy
copy /Y "%SRC%" "%RC%\altv-client.dll.orig.bak" >nul || goto :failcopy
if not exist "%RC%\patched" mkdir "%RC%\patched" >nul 2>&1
copy /Y "%SRC%" "%RC%\patched\altv-client.dll"  >nul || goto :failcopy

echo.
echo [INFO] Verifying installed copies (all must be %WANT_HASH%):
call :printhash "%RC%\altv-client.dll"
call :printhash "%RC%\altv-client.dll.orig.bak"
call :printhash "%RC%\patched\altv-client.dll"

echo.
echo [DONE] Client build set to genuine 16.4.39 (matches server).
echo        Now launch normally:  scripts\connect.cmd
echo        The WRONG_STABLE_BUILD kick should be gone.
pause
exit /b 0

:printhash
set "H="
for /f "skip=1 tokens=1 delims= " %%H in ('certutil -hashfile "%~1" SHA1') do if not defined H set "H=%%H"
echo   %H%  %~1
goto :eof

:failcopy
echo [ERROR] Copy failed (file locked?). Close GTA V / alt:V and retry.
pause
exit /b 1
