@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo .NET 8 SDK is required to publish.
 pause
 exit /b 1
)
dotnet publish "ArcaneVault.Desktop\ArcaneVault.Desktop.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0publish\win-x64"
if errorlevel 1 exit /b 1
echo.
echo Published: publish\win-x64\ArcaneVault.Desktop.exe
echo Microsoft Edge WebView2 Runtime is still required on the target machine.
pause
