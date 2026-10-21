@echo off
rem ---------------------------------------------------------------------------
rem  One-click fix: remove the "from the Internet" mark, trust the LGGZ
rem  certificate and verify the signatures of KeyboardOG.exe / HIDComm.dll.
rem
rem  Just double-click this file. It asks for administrator rights once
rem  (needed to install the certificate for all users).
rem ---------------------------------------------------------------------------
setlocal
chcp 65001 >nul

set "HERE=%~dp0"
set "PS=%HERE%Fix-Signature-And-Unblock.ps1"
if not exist "%PS%" set "PS=%HERE%signing\Fix-Signature-And-Unblock.ps1"

if not exist "%PS%" (
    echo [ERROR] Fix-Signature-And-Unblock.ps1 not found next to this script.
    pause
    exit /b 1
)

net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo Requesting administrator rights...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS%" %*
set "RC=%ERRORLEVEL%"

if "%~1"=="" pause
exit /b %RC%
