@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

rem ---- locate a working Python 3 (actually run it, do not trust where) ----
set "PY="
python -c "import sys" >nul 2>nul && set "PY=python"
if not defined PY ( py -3 -c "import sys" >nul 2>nul && set "PY=py -3" )
if not defined PY (
  if exist "C:\Users\hp\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe" (
    "C:\Users\hp\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe" -c "import sys" >nul 2>nul && set "PY=C:\Users\hp\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
  )
)
if not defined PY (
  echo [ERROR] No working Python 3 found.
  echo         Install Python 3, or edit the path inside this file.
  pause
  exit /b 1
)

echo Using: %PY%
echo Converting every .json in .\layout_json ...
echo.
%PY% kle2c.py %*
set "RC=%ERRORLEVEL%"
echo.
if "%RC%"=="0" (
  echo [OK] results are in .\output
) else (
  echo [FAIL] exit code %RC%
)
if "%~1"=="" pause
exit /b %RC%