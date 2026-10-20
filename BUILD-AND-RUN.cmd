@echo off
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Microsoft .NET 8 SDK is required to build VREC.
  echo Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
  echo Then run this file again.
  pause
  exit /b 1
)
dotnet build -c Release
if errorlevel 1 (
  echo Build failed. Copy the error messages for troubleshooting.
  pause
  exit /b 1
)
dotnet run -c Release --no-build
pause
