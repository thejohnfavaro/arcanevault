@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK is required. Build from Visual Studio or install the .NET 8 SDK.
  pause
  exit /b 1
)
set "OUT=%~dp0publish\single-exe"
dotnet publish "ArcaneVault.Desktop\ArcaneVault.Desktop.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None /p:DebugSymbols=false -o "%OUT%"
if errorlevel 1 (
  echo Publish failed; see errors above.
  pause
  exit /b 1
)
echo.
echo READY: "%OUT%\ArcaneVault.Desktop.exe"
echo Double-click this EXE. No separate web server or Python window is required.
echo Microsoft Edge WebView2 Runtime must be installed on target Windows machine.
pause
