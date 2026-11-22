# Friendly local regression launcher. Uses only disposable test fixtures;
# never selects or writes the user's native workspace or Arena history.
[CmdletBinding()]
param(
    [ValidateSet('Menu','DeckArtwork','MatchTracking','DeckEditing','DataSafety','Everything')]
    [string]$Area = 'Menu'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj'
$areas = [ordered]@{
    DeckArtwork = @{
        Label = 'Card art, saved deck covers and official printing metadata'
        Suites = 'native-deck-cover-cache,native-printing-artwork,native-visual-shell'
    }
    MatchTracking = @{
        Label = 'Arena match attribution, analytics and ranked queues'
        Suites = 'match-assignment,match-performance,match-deck-attribution,arena-log-assignment,arena-queue-rank'
    }
    DeckEditing = @{
        Label = 'Saving and editing decks without modifying owned cards'
        Suites = 'native-deck-editing,native-deck-library-cache,native-deck-analytics,native-deck-preflight'
    }
    DataSafety = @{
        Label = 'Collection safety, recovery and backup integrity'
        Suites = 'native-workspace-safety,native-crash-recovery,native-full-backup-export'
    }
    Everything = @{
        Label = 'Run every native regression check'
        Suites = ''
    }
}
if ($Area -eq 'Menu') {
    Write-Host ''
    Write-Host 'Arcane Vault - Guided Quality Checks' -ForegroundColor Cyan
    Write-Host 'These tests generate temporary example data; they do not edit your collection.'
    $keys = @($areas.Keys)
    for ($i = 0; $i -lt $keys.Count; $i++) {
        Write-Host ("{0}. {1}" -f ($i + 1), $areas[$keys[$i]].Label)
    }
    $response = Read-Host 'Enter a test number (or Q to cancel)'
    if ($response -match '^[qQ]$') { return }
    $number = 0
    if (-not [int]::TryParse($response,[ref]$number) -or
        $number -lt 1 -or $number -gt $keys.Count) {
        throw 'Please choose one of the numbers shown.'
    }
    $Area = $keys[$number - 1]
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 10 SDK is needed. Install it from https://dotnet.microsoft.com/download/dotnet/10.0'
}
$target = $areas[$Area]
Write-Host ''
Write-Host ("CHECKING: " + $target.Label) -ForegroundColor Cyan
Write-Host 'The app and personal collection are not modified by this test suite.'
$arguments = @('run','--project',$project,'-c','Release','--')
if ($target.Suites) { $arguments += @('--suite',$target.Suites) }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportFolder = Join-Path $env:TEMP 'ArcaneVault-GuidedChecks'
New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null
$report = Join-Path $reportFolder ("check-" + $Area + "-" + $stamp + ".log")
Write-Host ("Report: " + $report)
& dotnet @arguments 2>&1 | Tee-Object -FilePath $report
$exit = $LASTEXITCODE
Write-Host ''
if ($exit -eq 0) {
    Write-Host 'PASS - All selected automated checks completed.' -ForegroundColor Green
} else {
    Write-Host 'FAIL - At least one selected check failed. Please share the saved report.' -ForegroundColor Red
    Write-Host ("Report: " + $report)
    exit $exit
}
