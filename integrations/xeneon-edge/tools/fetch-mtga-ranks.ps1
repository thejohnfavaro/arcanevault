# Download the exact MTG Arena rank PNGs referenced by Draftsim's
# "What Are the Ranks in MTG Arena?" (March 11 2026).
# These are third-party game assets, not original Arcane Vault illustrations.
# CI fetches them for the widget package; do not alter or recolor the artwork.
param(
    [string]$Destination = "integrations/xeneon-edge/resources/mtga-ranks"
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path ".").Path
$out = Join-Path $root $Destination
New-Item -ItemType Directory -Path $out -Force | Out-Null
$tiers = @('bronze','silver','gold','platinum','diamond')
$lanes = @('constructed','limited')
$names = @()
foreach ($lane in $lanes) {
    foreach ($tier in $tiers) {
        foreach ($level in @(4,3,2,1)) {
            $names += "rank_${lane}_${tier}_t${level}.png"
        }
    }
    foreach ($special in @('mythic','unranked')) {
        $names += "rank_${lane}_${special}.png"
    }
}
if ($names.Count -ne 44) { throw "Expected 44 rank variants" }
foreach ($name in $names) {
    $uri = "https://draftsim.com/wp-content/uploads/2019/11/$name"
    $file = Join-Path $out $name
    $retrieved = $false
    for ($attempt = 0; $attempt -lt 3 -and -not $retrieved; $attempt++) {
        try {
            Invoke-WebRequest -Uri $uri -OutFile $file -MaximumRedirection 3 -TimeoutSec 30 -ErrorAction Stop
            $bytes = [System.IO.File]::ReadAllBytes($file)
            if ($bytes.Length -lt 128 -or $bytes.Length -gt 250000) { throw "Unexpected file size: $name" }
            $signature = @(137,80,78,71,13,10,26,10)
            for ($i=0; $i -lt 8; $i++) {
                if ($bytes[$i] -ne $signature[$i]) { throw "Not a PNG: $name" }
            }
            $retrieved = $true
        } catch {
            if (Test-Path $file) { Remove-Item $file -Force }
            if ($attempt -eq 2) { throw "Unable to retrieve verified MTGA icon $uri : $_" }
            Start-Sleep -Seconds (2 * ($attempt + 1))
        }
    }
}
Write-Host "Validated $($names.Count) original Constructed and Limited rank PNGs from Draftsim."
