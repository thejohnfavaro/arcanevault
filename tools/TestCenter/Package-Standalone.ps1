#requires -Version 5.1
# Package the standalone, offline GUI and its supporting scripts.
[CmdletBinding()]
param([string]$OutputPath='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $OutputPath){$OutputPath=Join-Path $root 'ArcaneVault-TestCenter-Standalone.zip'}
$required=@(
 'Open-TestCenter.ps1','Run-UiRegression.ps1','Run-UiSmoke.ps1',
 'Sync-TestCatalog.ps1','Test-CoverageMap.ps1','Tests-Catalog.json',
 'Coverage-Map.json','README.md'
)
foreach($name in $required){
 if(-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $name))){
   throw "Missing standalone package component: $name"
 }
}
$temp=Join-Path ([IO.Path]::GetTempPath()) ('arcane-test-center-'+[guid]::NewGuid().ToString('N'))
$package=Join-Path $temp 'ArcaneVault-TestCenter'
New-Item -ItemType Directory -Path $package -Force | Out-Null
try {
 foreach($name in $required){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $package}
 $launcher=@'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Open-TestCenter.ps1"
if errorlevel 1 pause
'@
 Set-Content -LiteralPath (Join-Path $package 'Open-TestCenter.cmd') -Value $launcher -Encoding ASCII
 $instructions=@'
Arcane Vault - Standalone Test Center

1. Extract this ZIP to a dedicated tools folder, outside your Arcane Vault data.
2. Double-click Open-TestCenter.cmd on Windows.
3. Use the "Desktop UI checks" tab with Arcane Vault already open.
4. To run native regression suites, choose a full source checkout on the first row.
5. Open Reports for local logs and screenshot evidence.

No live collection database is opened or saved by the Test Center runner.
Screenshot baselines and reports remain local.
'@
 Set-Content -LiteralPath (Join-Path $package 'START-HERE.txt') -Value $instructions -Encoding UTF8
 $destDir=Split-Path -Parent $OutputPath
 if($destDir -and -not (Test-Path $destDir)){New-Item -ItemType Directory -Path $destDir -Force | Out-Null}
 if(Test-Path -LiteralPath $OutputPath){Remove-Item -LiteralPath $OutputPath -Force}
 Compress-Archive -LiteralPath $package -DestinationPath $OutputPath -CompressionLevel Optimal
 if(-not (Test-Path -LiteralPath $OutputPath)){throw 'ZIP was not created.'}
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $zip=[IO.Compression.ZipFile]::OpenRead($OutputPath)
 try{
   $names=@($zip.Entries | ForEach-Object{$_.FullName})
   foreach($file in @('Open-TestCenter.cmd','Open-TestCenter.ps1','Run-UiRegression.ps1','Tests-Catalog.json')){
     if(-not (@($names|Where-Object{$_.Replace([char]92,[char]47).EndsWith("/$file") -or $_ -eq $file}).Count)){
       throw "Packaged ZIP missing $file"
     }
   }
 }finally{$zip.Dispose()}
 Write-Host "PASS: standalone Test Center packaged at $OutputPath"
}finally{
 Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
