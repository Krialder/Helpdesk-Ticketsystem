@echo off
chcp 65001 >nul
title Ticketsystem starten
echo ============================================
echo   Ticketsystem wird gestartet
echo ============================================
echo.

rem Ins Verzeichnis dieser Datei wechseln, damit der Start auch dann
rem funktioniert, wenn die Datei per Doppelklick von woanders aufgerufen wird.
pushd "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start.ps1"
set "EXITCODE=%ERRORLEVEL%"

popd

if not "%EXITCODE%"=="0" (
  echo.
  echo Es ist ein Problem aufgetreten ^(Code %EXITCODE%^).
  echo Der Grund steht oben: Das Startskript liest ihn aus der Protokolldatei
  echo und gibt ihn samt Pfad aus. Steht dort nichts, liegt das Protokoll unter
  echo %%LOCALAPPDATA%%\Ticketsystem\Protokoll\ ; dort die NEUESTE Datei nehmen,
  echo denn der Name traegt UTC und steht nach Mitternacht noch auf gestern.
  echo.
  echo Haeufigster Grund bei Buildfehlern nach einem Update: Das ZIP wurde ueber
  echo den alten Ordner entpackt. Entpacken loescht keine entfernten Dateien, und
  echo Reste einer alten Version brechen den Build. Abhilfe: den Ordner komplett
  echo loeschen und neu entpacken. Die Daten liegen im Benutzerprofil unter
  echo %%LOCALAPPDATA%%\Ticketsystem und bleiben dabei erhalten.
  echo.
  echo Dieses Fenster bleibt offen, damit die Meldung lesbar ist.
  pause >nul
)
