# Arcane Vault offline Test Center suite inventory sync.
# Windows PowerShell 5.1 and pwsh 7; no SDK or Internet required.
[CmdletBinding()]
param(
    [switch]$Check,
    [string]$SourcePath = '',
    [string]$CatalogPath = ''
)
$ErrorActionPreference = 'Stop'
$repository = if ($SourcePath) {
    [IO.Path]::GetFullPath($SourcePath)
} else {
    [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
}
if (-not $CatalogPath) { $CatalogPath = Join-Path $PSScriptRoot 'Tests-Catalog.json' }
$program = Join-Path $repository 'ArcaneVault.Next.Tests\Program.cs'
if (-not (Test-Path -LiteralPath $program -PathType Leaf)) {
    throw "Cannot find native test runner at $program. Supply -SourcePath to your checkout."
}
$code = [IO.File]::ReadAllText($program)
$regex = '(?m)^\s*\("([a-z0-9-]+)",\s*"([^"\r\n]+)"\),?\s*$'
$matches = [regex]::Matches($code, $regex)
if ($matches.Count -eq 0) { throw 'No suite descriptions found in Program.cs; test runner format may have changed.' }
$groups = [ordered]@{
    'Card catalog & art' = @('catalog-snapshot','native-scryfall-catalog-sync',
        'native-deck-cover-cache','native-theme','native-catalog-filter',
        'native-ownership-catalog','native-printing-artwork','native-card-role',
        'collection-catalog-review')
    'Game & match analytics' = @('match-assignment','match-performance',
        'match-deck-attribution','arena-log-assignment','arena-queue-rank',
        'arena-rank-season','native-deck-analytics')
    'Collection & synchronization' = @('ownership-golden','native-backup-reader',
        'native-large-collection','native-workspace','native-workspace-safety',
        'native-crash-recovery','native-fresh-start','native-physical-editing',
        'native-proxy-editing','native-physical-quantity','native-arena-editing',
        'native-arena-import','native-physical-valuation','native-shortlist',
        'native-excel-import','native-csv-import','native-full-backup-export',
        'native-peer-sync','native-set-progress')
    'Installation & compatibility' = @('migration-preview','offline-staging',
        'native-arena-presentation','native-update','native-visual-shell',
        'native-release-notes','legacy-storage-import')
    'Deck editor & EDH Lab' = @('native-deck-editing','native-deck-owned-picker',
        'native-deck-library-cache','native-deck-preflight','native-deck-interchange',
        'native-format-legality','edh-lab','edh-lab-draft','edh-lab-land-advisor',
        'edh-lab-mana-base-builder')
}
$previous = @{}
if (Test-Path -LiteralPath $CatalogPath -PathType Leaf) {
    $existing = Get-Content -LiteralPath $CatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($item in @($existing.tests)) {
        if ($item.id -and $item.category) { $previous[[string]$item.id] = [string]$item.category }
    }
}
function Get-Category([string]$Id) {
    foreach ($group in $groups.Keys) { if ($groups[$group] -contains $Id) { return $group } }
    if ($previous.ContainsKey($Id)) { return $previous[$Id] }
    if ($Id -match '^(edh-|native-deck-|native-format-)') { return 'Deck editor & EDH Lab' }
    if ($Id -match '(match|arena-log|arena-queue|arena-rank|analytics)') { return 'Game & match analytics' }
    if ($Id -match '(catalog|artwork|scryfall|theme|card-role)') { return 'Card catalog & art' }
    if ($Id -match '(import|install|update|release|migration|offline|visual-shell)') { return 'Installation & compatibility' }
    return 'Collection & synchronization'
}
$tests = @()
$seen = @{}
foreach ($match in $matches) {
    $id = $match.Groups[1].Value
    if ($seen.ContainsKey($id)) { throw "Duplicate named test suite in Program.cs: $id" }
    $seen[$id] = $true
    $tests += [ordered]@{id=$id; label=$match.Groups[2].Value;
        category=(Get-Category $id); kind='native-suite'; manual=$false}
}
$document = [ordered]@{
    schema_version=1
    test_center_version='0.6.0'
    based_on='ArcaneVault.Next.Tests/Program.cs (generated; do not edit by hand)'
    tests=$tests
}
if ($Check) {
    if (-not (Test-Path -LiteralPath $CatalogPath -PathType Leaf)) {
        Write-Error "Missing Test Center catalog: $CatalogPath. Run Sync-TestCatalog.ps1 without -Check."
        exit 1
    }
    $actual = Get-Content -LiteralPath $CatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $actualRows = @($actual.tests)
    $different = $actual.schema_version -ne 1 -or
        $actualRows.Count -ne $tests.Count -or
        $actual.test_center_version -ne $document.test_center_version
    if (-not $different) {
        for ($i=0; $i -lt $tests.Count; $i++) {
            $a=$actualRows[$i]; $b=$tests[$i]
            if ($a.id -cne $b.id -or $a.label -cne $b.label -or
                $a.category -cne $b.category -or $a.kind -cne $b.kind -or $a.manual -ne $false) {
                $different = $true; break
            }
        }
    }
    if ($different) {
        Write-Host 'FAIL: Test Center catalog is out of date with the C# runner.' -ForegroundColor Red
        Write-Host 'Run tools\TestCenter\Sync-TestCatalog.ps1, commit the catalog, and rerun.' 
        exit 1
    }
    Write-Host "PASS: Test Center catalog matches all $($tests.Count) native suites." -ForegroundColor Green
    return
}
$dir = Split-Path -Parent $CatalogPath
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$json = $document | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText([IO.Path]::GetFullPath($CatalogPath),
    $json + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Updated $CatalogPath ($($tests.Count) suites)."
