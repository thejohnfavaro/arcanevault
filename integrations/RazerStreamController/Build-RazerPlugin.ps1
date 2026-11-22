param([switch]$Package)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'ArcaneVaultRazer.csproj'
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
if ($Package) {
    if (-not (Get-Command logiplugintool -ErrorAction SilentlyContinue)) {
        throw 'Install the current SDK packaging tool: dotnet tool install -g LogiPluginTool'
    }
    $output = Join-Path $root 'bin\Release'
    $file = Join-Path $root 'ArcaneVaultRazer_0_1_0.lplug4'
    logiplugintool pack $output $file
    if ($LASTEXITCODE -ne 0) { throw 'Plugin packaging failed.' }
    logiplugintool verify $file
    if ($LASTEXITCODE -ne 0) { throw 'Package verification failed.' }
    Write-Host "Package ready: $file"
}
