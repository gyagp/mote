@echo off
cd /d "%~dp0"
if exist "dist\Mote.exe" (
  start "" "dist\Mote.exe"
  exit /b
)
dotnet build Mote.csproj -c Release --nologo
if errorlevel 1 (
  pause
  exit /b 1
)
start "" "bin\Release\net8.0-windows\Mote.exe"
