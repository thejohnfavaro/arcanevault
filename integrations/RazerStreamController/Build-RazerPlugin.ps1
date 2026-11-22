param([string]$PluginApiDir = "", [switch]$Package)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $PluginApiDir) {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Logi\LogiPluginService'),
        (Join-Path $env:ProgramFiles 'Loupedeck\Loupedeck2')
    )
    $PluginApiDir = $candidates | Where-Object {
        Test-Path (Join-Path $_ 'PluginApi.dll')
    } | Select-Object -First 1
}
if (-not $PluginApiDir -or -not (Test-Path (Join-Path $PluginApiDir 'PluginApi.dll'))) {
    throw 'PluginApi.dll not found. Pass -PluginApiDir pointing to the directory containing the official SDK DLL.'
}
$project = Join-Path $root 'ArcaneVaultRazer.csproj'
$dir = (Resolve-Path $PluginApiDir).Path.TrimEnd('\') + '\'
dotnet build $project -c Release -p:PluginApiDir="$dir"
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
if ($Package) {
    if (-not (Get-Command logiplugintool -ErrorAction SilentlyContinue)) {
        throw 'Install with: dotnet tool install -g Loupedeck.PluginTool'
    }
    $output = Join-Path $root 'bin\Release'
    $file = Join-Path $root 'ArcaneVaultRazer_0_1_0.lplug4'
    logiplugintool pack $output $file
    if ($LASTEXITCODE -ne 0) { throw 'Plugin packaging failed.' }
    logiplugintool verify $file
    if ($LASTEXITCODE -ne 0) { throw 'Package verification failed.' }
    Write-Host "Package ready: $file"
}
