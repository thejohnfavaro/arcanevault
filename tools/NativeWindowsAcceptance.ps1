# Arcane Vault Native Windows — read-only release acceptance fingerprint.
# Run with the native desktop app CLOSED. Does not open/write SQLite,
# query cards, modify the installed legacy application, or upload data.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Capture', 'Compare')]
    [string]$Mode,
    [string]$ReportPath,
    [string]$BaselinePath,
    [string]$WorkspaceDirectory = (Join-Path $env:LOCALAPPDATA 'ArcaneVaultNext')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Canonical([string]$Path) {
    return [System.IO.Path]::GetFullPath(
        [Environment]::ExpandEnvironmentVariables($Path))
}
function IsInside([string]$Child, [string]$Parent) {
    $root = $Parent.TrimEnd('\','/') + [System.IO.Path]::DirectorySeparatorChar
    return $Child.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($Child, $Parent, [StringComparison]::OrdinalIgnoreCase)
}
function Fingerprint([string]$Directory) {
    if (-not [System.IO.Directory]::Exists($Directory)) {
        throw "Native data directory does not exist. Nothing has been changed."
    }
    $workspace = Join-Path $Directory 'native-workspace.sqlite'
    if (-not [System.IO.File]::Exists($workspace)) {
        throw "Native workspace does not exist. Import/create a native collection first."
    }
    $files = @(
        Get-ChildItem -LiteralPath $Directory -File -ErrorAction Stop |
            Where-Object {
                $_.Name -match '^native-(workspace|catalog)(?:[.\-][a-zA-Z0-9_.\-]+)?\.sqlite$'
            } |
            Sort-Object Name
    )
    if ($files.Count -gt 300) {
        throw "Over 300 native workspace/catalog/recovery SQLite files; review manually."
    }
    $rows = @()
    foreach ($item in $files) {
        if ($item.Length -gt 6000000000) {
            throw "A native SQLite file exceeds the 6 GB diagnostic cap."
        }
        $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $rows += [pscustomobject]@{
            Name   = [string]$item.Name
            Bytes  = [long]$item.Length
            Sha256 = [string]$hash
        }
    }
    return [pscustomobject]@{
        Schema = 1
        GeneratedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        FileCount = [int]$rows.Count
        Files = @($rows)
    }
}

$source = Canonical $WorkspaceDirectory
$running = @(Get-Process -Name 'ArcaneVault.Next.Desktop' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw "Close every running Arcane Vault Native window first. No files were changed."
}

if ($Mode -eq 'Capture') {
    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        throw 'Capture requires -ReportPath outside the native data folder.'
    }
    $out = Canonical $ReportPath
    if (IsInside $out $source) {
        throw 'ReportPath must be OUTSIDE the native workspace. No files were changed.'
    }
    if ([System.IO.File]::Exists($out)) {
        throw 'An acceptance baseline already exists at ReportPath; refusing to overwrite.'
    }
    $parent = [System.IO.Path]::GetDirectoryName($out)
    if (-not [System.IO.Directory]::Exists($parent)) {
        throw 'The report parent folder must already exist.'
    }
    $data = Fingerprint $source
    $json = $data | ConvertTo-Json -Depth 6
    $stream = [System.IO.File]::Open($out, [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($json)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    } finally {
        $stream.Dispose()
    }
    Write-Host "CAPTURED: $($data.FileCount) native SQLite files; no collection contents exported."
    Write-Host 'Keep this local baseline and your separate FULL collection backup.'
    return
}

if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    throw 'Compare requires -BaselinePath pointing to an earlier captured report.'
}
$baselineFile = Canonical $BaselinePath
if (IsInside $baselineFile $source) {
    throw 'BaselinePath must be outside the native workspace.'
}
$info = Get-Item -LiteralPath $baselineFile -ErrorAction Stop
if ($info.Length -le 0 -or $info.Length -gt 300000) {
    throw 'Invalid or oversized acceptance baseline.'
}
$baseline = Get-Content -LiteralPath $baselineFile -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ($baseline.Schema -ne 1 -or $baseline.FileCount -lt 1 -or
    $baseline.FileCount -gt 300) {
    throw 'Unsupported acceptance baseline.'
}
$now = Fingerprint $source
$before = @($baseline.Files)
$after = @($now.Files)
if ($before.Count -ne $baseline.FileCount -or
    $before.Count -ne $after.Count) {
    throw "ACCEPTANCE MISMATCH: native SQLite file count changed (before $($before.Count), now $($after.Count)). Review before 1.0."
}
for ($i = 0; $i -lt $before.Count; $i++) {
    $old = $before[$i]
    $new = $after[$i]
    if ($old.Name -cne $new.Name -or
        [long]$old.Bytes -ne [long]$new.Bytes -or
        [string]$old.Sha256 -cne [string]$new.Sha256) {
        throw "ACCEPTANCE MISMATCH: $($old.Name) changed. This may be a legitimate edit or SQLite maintenance, but must be explained before 1.0."
    }
}
Write-Host "PASS: $($now.FileCount) native workspace, catalog and recovery files remain byte-identical."
Write-Host 'This is a byte-integrity check only: also verify the actual collection and UI manually.'
