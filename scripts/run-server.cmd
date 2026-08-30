@echo off
set "HERE=%~dp0"
powershell -ExecutionPolicy Bypass -File "%HERE%run-server.ps1" %*

