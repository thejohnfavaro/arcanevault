<#
.SYNOPSIS
  Validate, package and publish a native Windows preview when GitHub Actions is stuck.
.DESCRIPTION
  Uses the same .NET 10 native acceptance tests, standalone publish options,
  version manifest, zip asset name and SHA-256 sidecar as CI.
  Requires Windows, Git, .NET SDK 10 and an authenticated GitHub CLI.
  Never edits the native workspace, physical collection or Arena collection.
#>
[CmdletBinding()]
param(
    [switch]$PackageOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE"
    }
}

$repo = 'thejohnfavaro/arcanevault'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    foreach ($binary in @('git', 'dotnet', 'gh')) {
        if (-not (Get-Command $binary -ErrorAction SilentlyContinue)) {
            throw "Missing required command: $binary. Install it before running this script."
        }
    }

    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') {
        throw 'Switch to the main branch before making a native release.'
    }
    $dirty = @(& git status --porcelain)
    if ($LASTEXITCODE -ne 0 -or $dirty.Count -gt 0) {
        throw 'Working tree must be clean before creating a verifiable release.'
    }

    $sha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sha -notmatch '^[0-9a-f]{40}$') {
        throw 'Could not determine the source commit.'
    }
    Invoke-Checked git @('fetch', 'origin', 'main')
    $remoteSha = (& git rev-parse 'origin/main').Trim()
    if ($sha -ne $remoteSha) {
        throw "Local main is not the current origin/main. Run git pull --ff-only and retry."
    }

    Invoke-Checked gh @('auth', 'status')
    [xml]$project = Get-Content 'ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj'
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid native version: $version" }
    $tag = "native-v$version"
    $notes = Join-Path $root "releases/$tag.md"
    if (-not (Test-Path $notes)) { throw "Missing release notes: $notes" }
    $body = Get-Content -Raw $notes
    foreach ($section in @('Added', 'Changed', 'Fixed', 'Removed')) {
        if ($body -notmatch "(?m)^## $section\s*$") {
            throw "Release notes are missing required section ## $section"
        }
    }

    # "gh release view" emits a native stderr error for a missing tag.
    # Windows PowerShell 5.1 escalates that to a terminating NativeCommandError
    # when $ErrorActionPreference = 'Stop'. List tags instead: a missing
    # release is an expected condition, not a failure.
    $existingJson = & gh release list --repo $repo --limit 1000 --json tagName
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect existing GitHub releases; refusing to publish blindly."
    }
    $existing = @($existingJson | ConvertFrom-Json)
    if ($existing | Where-Object { $_.tagName -eq $tag }) {
        throw "Release $tag already exists; refusing to overwrite its assets."
    }

    Write-Host "Validating native v$version at commit $sha" -ForegroundColor Cyan
    Invoke-Checked dotnet @('restore', 'ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj')
    Invoke-Checked dotnet @('run', '--project',
        'ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj',
        '-c', 'Release', '--no-restore')

    $stageRoot = Join-Path $env:TEMP "ArcaneVaultNativeRelease-$version-$($sha.Substring(0,8))"
    $publish = Join-Path $stageRoot 'standalone'
    $package = Join-Path $stageRoot 'package'
    if (Test-Path $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $publish, $package -Force | Out-Null

    Invoke-Checked dotnet @('publish', 'ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj',
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-o', $publish)
    $exe = Join-Path $publish 'ArcaneVault.Next.Desktop.exe'
    if (-not (Test-Path $exe)) { throw "Missing published EXE: $exe" }

    $toolsFolder = Join-Path $publish 'Tools'
    New-Item -ItemType Directory -Path $toolsFolder -Force | Out-Null
    Copy-Item 'tools/NativeWindowsAcceptance.ps1' (Join-Path $toolsFolder 'NativeWindowsAcceptance.ps1')

    # Windows PowerShell 5.1's "Set-Content -Encoding UTF8" emits an
    # EF BB BF BOM, which older native updater builds reject as JSON.
    # Write UTF-8 without a BOM on all supported PowerShell versions.
    $manifestPath = Join-Path $publish 'native-version.json'
    $manifestJson = @{ version = $version; sourceSha = $sha; channel = 'preview' } |
        ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($manifestPath, $manifestJson,
        (New-Object System.Text.UTF8Encoding($false)))

    $zip = Join-Path $package 'ArcaneVaultNative-win-x64.zip'
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $sidecar = "$zip.sha256"
    Set-Content -Path $sidecar -Value "$hash  ArcaneVaultNative-win-x64.zip" -Encoding Ascii

    $verifyDir = Join-Path $stageRoot 'verify'
    Expand-Archive -Path $zip -DestinationPath $verifyDir
    foreach ($file in @('ArcaneVault.Next.Desktop.exe', 'Tools/NativeWindowsAcceptance.ps1',
            'native-version.json')) {
        if (-not (Test-Path (Join-Path $verifyDir $file))) {
            throw "Archive does not contain $file"
        }
    }
    # Verify raw bytes too: ConvertFrom-Json tolerates a BOM, but older
    # native updater versions do not.
    $manifestRaw = [System.IO.File]::ReadAllBytes((Join-Path $verifyDir 'native-version.json'))
    if ($manifestRaw.Length -ge 3 -and
        $manifestRaw[0] -eq 0xEF -and $manifestRaw[1] -eq 0xBB -and
        $manifestRaw[2] -eq 0xBF) {
        throw 'Native update JSON unexpectedly contains UTF-8 BOM.'
    }
    $manifest = Get-Content -Raw (Join-Path $verifyDir 'native-version.json') | ConvertFrom-Json
    if ($manifest.version -ne $version -or $manifest.sourceSha -ne $sha -or
            $manifest.channel -ne 'preview') { throw 'Version manifest validation failed.' }
    if ((Get-FileHash -Path $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) {
        throw 'SHA-256 mismatch.'
    }

    Write-Host "Verified local package: $zip" -ForegroundColor Green
    Write-Host "Checksum: $hash"
    if ($PackageOnly) {
        Write-Host 'PackageOnly: no GitHub release created.'
    }
    else {
        Write-Host "Publishing prerelease $tag to $repo..." -ForegroundColor Cyan
        Invoke-Checked gh @('release', 'create', $tag, $zip, $sidecar,
            '--repo', $repo, '--target', $sha,
            '--title', "Arcane Vault Native Windows v$version (Preview)",
            '--notes-file', $notes, '--prerelease')
        Invoke-Checked gh @('release', 'view', $tag, '--repo', $repo)
        Write-Host "Release available: https://github.com/$repo/releases/tag/$tag" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
