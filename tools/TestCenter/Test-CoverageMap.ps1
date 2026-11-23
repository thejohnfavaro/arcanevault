# Validates curated system-to-suite evidence, not runtime test outcomes.
# Windows PowerShell 5.1 / pwsh 7. No network, SDK, or native workspace access.
[CmdletBinding()]
param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot 'Tests-Catalog.json'),
    [string]$CoveragePath = (Join-Path $PSScriptRoot 'Coverage-Map.json')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CatalogPath)) { throw "Missing test suite inventory: $CatalogPath" }
if (-not (Test-Path -LiteralPath $CoveragePath)) { throw "Missing coverage map: $CoveragePath" }
$catalog = Get-Content -LiteralPath $CatalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$map = Get-Content -LiteralPath $CoveragePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($map.schema_version -ne 1) { throw 'Unsupported coverage map schema.' }
$ids = @($catalog.tests | ForEach-Object { [string]$_.id })
$mapped = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$systems = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$errors = New-Object 'System.Collections.Generic.List[string]'
foreach ($system in @($map.systems)) {
    if ([string]::IsNullOrWhiteSpace([string]$system.id) -or
        [string]::IsNullOrWhiteSpace([string]$system.system) -or
        [string]::IsNullOrWhiteSpace([string]$system.evidence) -or
        [string]::IsNullOrWhiteSpace([string]$system.manual_check) -or
        [string]::IsNullOrWhiteSpace([string]$system.gap)) {
        $errors.Add("Incomplete coverage mapping: $($system.id)")
    }
    if (-not $systems.Add([string]$system.id)) {
        $errors.Add("Duplicate system ID: $($system.id)")
    }
    if ($system.evidence_level -notin @('behavioral','source-plus-behavior','manual-only')) {
        $errors.Add("Invalid evidence level: $($system.id)")
    }
    if ($system.evidence_level -ne 'manual-only' -and @($system.suite_ids).Count -eq 0) {
        $errors.Add("No named test suites for $($system.id); use manual-only if appropriate.")
    }
    foreach ($suite in @($system.suite_ids)) {
        if ($ids -notcontains [string]$suite) {
            $errors.Add("Unknown suite $suite mapped to $($system.id)")
        } else { [void]$mapped.Add([string]$suite) }
    }
}
$unmapped = @($ids | Where-Object { -not $mapped.Contains($_) })
if ($unmapped.Count -gt 0) {
    $errors.Add('Unmapped named suites require manual review: ' + ($unmapped -join ', '))
}
if ($errors.Count -gt 0) {
    foreach ($message in $errors) { Write-Host ('FAIL: ' + $message) -ForegroundColor Red }
    exit 1
}
Write-Host ("PASS: $(@($map.systems).Count) reviewed systems, $($ids.Count) catalog suites mapped; manual gaps remain explicit.") -ForegroundColor Green
