@echo off
chcp 65001 >nul
cd /d "%~dp0"

set "EXE=KLEconvert\bin\Debug\KLEconvert.exe"
if exist "%EXE%" goto run

echo Building...
set "MSB="
where msbuild >nul 2>nul && set "MSB=msbuild"
if not defined MSB (
  if exist "D:\Microsoft VS\MSBuild\Current\Bin\MSBuild.exe" set "MSB=D:\Microsoft VS\MSBuild\Current\Bin\MSBuild.exe"
)
if not defined MSB (
  if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set "MSB=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not defined MSB (
  echo [ERROR] MSBuild not found. Open KLEconvert.sln in Visual Studio instead.
  pause
  exit /b 1
)
"%MSB%" KLEconvert\KLEconvert.csproj /t:Build /p:Configuration=Debug /v:minimal /nologo
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)

:run
start "" "%EXE%" %*
exit /b 0