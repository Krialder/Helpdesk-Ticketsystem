@echo off
chcp 65001 >nul
title Ticketsystem veroeffentlichen
pushd "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\veroeffentlichen.ps1"
set "EXITCODE=%ERRORLEVEL%"
popd
echo.
echo Dieses Fenster bleibt offen, damit die Meldungen lesbar sind.
pause >nul
exit /b %EXITCODE%
