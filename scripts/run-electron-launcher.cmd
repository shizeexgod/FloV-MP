@echo off
title Derzhava RP Launcher
cd /d "%~dp0..\launcher\electron"
start "" "%~dp0..\launcher\electron\node_modules\electron\dist\electron.exe" .
