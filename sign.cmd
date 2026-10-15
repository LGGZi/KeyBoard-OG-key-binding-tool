@echo off
rem One-click signing: build Release -^> sign -^> verify.
rem Double-click this file after changing code.
rem Only sign (skip build):  sign.cmd -NoBuild
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0signing\Sign-OneClick.ps1" %*
echo.
pause