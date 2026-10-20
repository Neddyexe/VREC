@echo off
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
dotnet build VREC.csproj -c Release
if errorlevel 1 (
  pause
  exit /b 1
)
start "" "bin\Release\net8.0-windows\VREC.exe"
