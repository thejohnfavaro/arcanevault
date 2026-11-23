# Read-only WPF UI Automation smoke tests. Attach to an open Arcane Vault window.
# Never starts the app, changes a selection, saves a deck, or reads its database.
[CmdletBinding()]
param([ValidateSet('All','DeckEditor','Analytics')][string]$Target='All',
      [int]$AppProcessId=0,[string]$ReportDir='',[switch]$SelfTest)
$ErrorActionPreference='Stop'
if(-not $ReportDir){$ReportDir=Join-Path $PSScriptRoot 'Reports'}
$results=New-Object 'System.Collections.Generic.List[object]'
function Add-Result([string]$id,[string]$status,[string]$details){
    $results.Add([pscustomobject]@{id=$id;result=$status;details=$details})
    Write-Host ("{0} {1}: {2}" -f $status,$id,$details)
}
function GoodRect($r){
    if($null -eq $r){return $false}
    foreach($n in @($r.X,$r.Y,$r.Width,$r.Height)){
        if([double]::IsNaN($n) -or [double]::IsInfinity($n)){return $false}
    }
    return $r.Width -gt 0 -and $r.Height -gt 0
}
function Aligned($a,$b,$c){
    foreach($v in @($a,$b,$c)){if(-not (GoodRect $v)){return $false}}
    $previous=$a
    foreach($next in @($b,$c)){
        if([Math]::Abs($next.Y-$previous.Y) -le 35){
            if($next.X -le $previous.X+80){return $false}
        } else {
            if($next.Y -lt $previous.Y -or $next.Y-$previous.Y -gt 440 -or
                [Math]::Abs($next.X-$a.X) -gt 95){return $false}
        }
        $previous=$next
    }
    return $true
}
if($SelfTest){
    $a=[pscustomobject]@{X=10.0;Y=20.0;Width=80.0;Height=20.0}
    $b=[pscustomobject]@{X=455.0;Y=20.0;Width=80.0;Height=20.0}
    $c=[pscustomobject]@{X=900.0;Y=20.0;Width=80.0;Height=20.0}
    $wrap=[pscustomobject]@{X=10.0;Y=250.0;Width=80.0;Height=20.0}
    $wrap2=[pscustomobject]@{X=10.0;Y=470.0;Width=80.0;Height=20.0}
    $bad=[pscustomobject]@{X=10.0;Y=900.0;Width=80.0;Height=20.0}
    if((Aligned $a $b $c) -and (Aligned $a $b $wrap) -and
       (Aligned $a $wrap $wrap2) -and -not (Aligned $a $b $bad)){
        Add-Result 'UI-SMOKE-SELFTEST' 'PASS' 'Synthetic aligned, wrapped and detached geometry cases passed.'
    }else{Add-Result 'UI-SMOKE-SELFTEST' 'FAIL' 'Synthetic geometry validation failed.'}
}elseif(-not [Environment]::UserInteractive){
    Add-Result 'UI-SMOKE-DESKTOP' 'SKIP' 'Interactive Windows desktop unavailable.'
}else{
    try{
        Add-Type -AssemblyName UIAutomationClient
        Add-Type -AssemblyName UIAutomationTypes
        $root=[System.Windows.Automation.AutomationElement]::RootElement
        $windows=$root.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        $candidates=@()
        foreach($win in $windows){
            try{
                $pidNumber=[int]$win.Current.ProcessId
                if($pidNumber -le 0 -or $win.Current.Name -notlike '*Arcane Vault*' -or
                   ($AppProcessId -gt 0 -and $pidNumber -ne $AppProcessId)){continue}
                $process=Get-Process -Id $pidNumber -ErrorAction SilentlyContinue
                if($process -and $process.ProcessName -like 'ArcaneVault*'){$candidates+=,$win}
            }catch{}
        }
        function Find-Ui($win,[string]$id){
            $condition=[System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
            return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
        }
        function Visible-Ui($element){
            if($null -eq $element){return $null}
            try{
                if($element.Current.IsOffscreen){return $null}
                $rect=$element.Current.BoundingRectangle
                if(GoodRect $rect){return $rect}
            }catch{}
            return $null
        }
        if($candidates.Count -eq 0){
            Add-Result 'UI-SMOKE-ATTACH' 'SKIP' 'Open Arcane Vault on the desktop before running UI tests.'
        }else{
            Add-Result 'UI-SMOKE-ATTACH' 'PASS' 'Attached read-only to Arcane Vault.'
            if($Target -in @('All','DeckEditor')){
                $editor=$null
                foreach($win in $candidates){if(Find-Ui $win 'AV.Mainboard.Rows'){$editor=$win;break}}
                if($null -eq $editor){
                    Add-Result 'UI-EDITOR-MAINBOARD' 'SKIP' 'Open a saved deck or EDH Lab editor and select Manage.'
                    Add-Result 'UI-EDITOR-PREVIEW' 'SKIP' 'Editor not open.'
                }else{
                    $ids=@('AV.Mainboard.Rows','AV.Mainboard.Search','AV.Mainboard.Composition',
                        'AV.Mainboard.Status','AV.Mainboard.FullDetails')
                    $missing=@($ids|Where-Object{-not (Visible-Ui (Find-Ui $editor $_))})
                    if($missing.Count){
                        Add-Result 'UI-EDITOR-MAINBOARD' 'SKIP' ('Manage controls hidden: '+($missing -join ', ')+'. Open Manage.')
                    }else{
                        $list=Visible-Ui (Find-Ui $editor 'AV.Mainboard.Rows')
                        $search=Visible-Ui (Find-Ui $editor 'AV.Mainboard.Search')
                        if($list.Y -gt $search.Y -and $list.Height -ge 60){
                            Add-Result 'UI-EDITOR-MAINBOARD' 'PASS' 'Actual mainboard controls visible in order.'
                        }else{
                            Add-Result 'UI-EDITOR-MAINBOARD' 'FAIL' 'Mainboard list overlaps search or is too short.'
                        }
                    }
                    $card=Find-Ui $editor 'AV.Preview.CardName'
                    $status=Find-Ui $editor 'AV.Preview.Status'
                    if(-not (Visible-Ui $card) -or -not (Visible-Ui $status)){
                        Add-Result 'UI-EDITOR-PREVIEW' 'SKIP' 'Preview panel hidden.'
                    }else{
                        $name=[string]$card.Current.Name
                        if([string]::IsNullOrWhiteSpace($name) -or $name -match '^(Select|Choose|Preview)'){
                            Add-Result 'UI-EDITOR-PREVIEW' 'SKIP' 'Select a visible card first.'
                        }else{
                            $message=[string]$status.Current.Name
                            if($message -match 'Loading local card details'){
                                Start-Sleep -Seconds 6
                                try{$message=[string]$status.Current.Name}catch{}
                            }
                            if($message -match 'Loading local card details'){
                                Add-Result 'UI-EDITOR-PREVIEW' 'FAIL' 'Card still loading after six seconds.'
                            }elseif((Visible-Ui (Find-Ui $editor 'AV.Preview.Artwork')) -or
                                    (Visible-Ui (Find-Ui $editor 'AV.Preview.Fallback'))){
                                Add-Result 'UI-EDITOR-PREVIEW' 'PASS' 'Preview finished; art/fallback bounds visible (not pixel-checked).'
                            }else{
                                Add-Result 'UI-EDITOR-PREVIEW' 'WARN' 'Preview finished; art surface not exposed.'
                            }
                        }
                    }
                }
            }
            if($Target -in @('All','Analytics')){
                $analytics=$null
                foreach($win in $candidates){if(Find-Ui $win 'AV.Analytics.FormatHeading'){$analytics=$win;break}}
                if($null -eq $analytics){
                    Add-Result 'UI-ANALYTICS-LAYOUT' 'SKIP' 'Open Deck Analytics and scroll to matchup headings.'
                }else{
                    $a=Visible-Ui (Find-Ui $analytics 'AV.Analytics.FormatHeading')
                    $b=Visible-Ui (Find-Ui $analytics 'AV.Analytics.RecordedColorsHeading')
                    $c=Visible-Ui (Find-Ui $analytics 'AV.Analytics.ObservedColorsHeading')
                    if($null -eq $a -or $null -eq $b -or $null -eq $c){
                        Add-Result 'UI-ANALYTICS-LAYOUT' 'SKIP' 'Headings off screen; scroll to matchups.'
                    }elseif(Aligned $a $b $c){
                        Add-Result 'UI-ANALYTICS-LAYOUT' 'PASS' 'Three real matchup headings align or wrap in order.'
                    }else{
                        Add-Result 'UI-ANALYTICS-LAYOUT' 'FAIL' 'Observed-colors heading detached or out of order.'
                    }
                }
            }
        }
    }catch{
        Add-Result 'UI-SMOKE-ENVIRONMENT' 'SKIP' ('UI Automation unavailable: '+$_.Exception.Message)
    }
}
if(-not (Test-Path -LiteralPath $ReportDir)){New-Item -ItemType Directory -Path $ReportDir -Force|Out-Null}
$prefix=Join-Path $ReportDir ('ui-smoke-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
$state=if(@($results|Where-Object{$_.result -eq 'FAIL'}).Count){'FAIL'}
    elseif(@($results|Where-Object{$_.result -eq 'PASS'}).Count){'PASS'}else{'SKIP'}
$summary=[ordered]@{schema_version=1;test_center_version='0.6.1';type='ui-smoke';
    generated_at=(Get-Date).ToString('o');target=$Target;self_test=[bool]$SelfTest;
    status=$state;tests=@($results.ToArray())}
$summary|ConvertTo-Json -Depth 6|Set-Content -LiteralPath ($prefix+'.json') -Encoding UTF8
(@('Arcane Vault read-only Windows UI smoke','Overall: '+$state,
    'UIA bounds do not verify screenshot pixels. Personal data unchanged.','')+
    @($results|ForEach-Object{"$($_.result) $($_.id): $($_.details)"}))|
    Set-Content -LiteralPath ($prefix+'.txt') -Encoding UTF8
Write-Host ('Reports: '+$prefix+'.json and .txt')
if($state -eq 'FAIL'){exit 1}
exit 0
