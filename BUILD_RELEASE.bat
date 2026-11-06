@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK was not found. Install it or build in Visual Studio.
  pause
  exit /b 1
)
dotnet restore "ArcaneVault.sln" || exit /b 1
dotnet build "ArcaneVault.sln" -c Release --no-restore || exit /b 1
echo.
echo Build complete: ArcaneVault.Desktop\bin\Release\net8.0-windows\
pause
