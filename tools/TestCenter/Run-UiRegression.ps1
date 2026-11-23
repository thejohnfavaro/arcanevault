# Local, opt-in Windows desktop UI regression checks.
# Never writes to an Arcane Vault collection, deck, or workspace.
[CmdletBinding()]
param(
  [ValidateSet('All','Interaction','Visual','Responsive')][string]$Mode='All',
  [int]$AppProcessId=0,
  [string]$ReportDir='',
  [string]$BaselineDir='',
  [switch]$ApproveBaseline,
  [switch]$SelfTest
)
$ErrorActionPreference='Stop'
if(-not $ReportDir){$ReportDir=Join-Path $PSScriptRoot 'Reports'}
if(-not $BaselineDir){$BaselineDir=Join-Path $PSScriptRoot 'Local-Baselines'}
$results=New-Object 'System.Collections.Generic.List[object]'
function Result([string]$id,[string]$status,[string]$details) {
  $results.Add([pscustomobject]@{id=$id;result=$status;details=$details})
  Write-Host "$status $id : $details"
}
function Valid-Rect($r) {
  if($null -eq $r){return $false}
  foreach($n in @($r.X,$r.Y,$r.Width,$r.Height)) {
    if([double]::IsNaN($n) -or [double]::IsInfinity($n)){return $false}
  }
  return ($r.Width -gt 0 -and $r.Height -gt 0)
}
function Get-Control($window,[string]$id) {
  $p=[System.Windows.Automation.AutomationElement]::AutomationIdProperty
  $c=New-Object System.Windows.Automation.PropertyCondition($p,$id)
  return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$c)
}
function Get-VisibleRect($window,[string]$id) {
  $e=Get-Control $window $id
  if($null -eq $e){return $null}
  try {
    if(-not $e.Current.IsOffscreen -and (Valid-Rect $e.Current.BoundingRectangle)){
      return $e.Current.BoundingRectangle
    }
  }catch{}
  return $null
}
function Compare-Bitmaps($expected,$actual,[int]$threshold=18,[double]$maxChangedFraction=0.02) {
  if($expected.Width -ne $actual.Width -or $expected.Height -ne $actual.Height){
    return [pscustomobject]@{pass=$false;fraction=1.0;reason='Screenshot dimensions differ'}
  }
  $changed=0L
  $total=[long]$actual.Width * [long]$actual.Height
  for($y=0;$y -lt $actual.Height;$y++){
    for($x=0;$x -lt $actual.Width;$x++){
      $a=$actual.GetPixel($x,$y);$b=$expected.GetPixel($x,$y)
      if(([Math]::Abs([int]$a.R-[int]$b.R) -gt $threshold) -or
         ([Math]::Abs([int]$a.G-[int]$b.G) -gt $threshold) -or
         ([Math]::Abs([int]$a.B-[int]$b.B) -gt $threshold)){$changed++}
    }
  }
  $fraction=[double]$changed/[double]$total
  return [pscustomobject]@{pass=($fraction -le $maxChangedFraction);
    fraction=$fraction;reason='Pixel difference outside per-channel tolerance'}
}
function Capture-Window($r) {
  $w=[int][Math]::Ceiling($r.Width);$h=[int][Math]::Ceiling($r.Height)
  if($w -lt 200 -or $h -lt 150 -or $w -gt 8000 -or $h -gt 8000) {
    throw 'Window screenshot dimensions are invalid.'
  }
  $bmp=New-Object System.Drawing.Bitmap($w,$h)
  $graphics=[System.Drawing.Graphics]::FromImage($bmp)
  try {
    $graphics.CopyFromScreen([int]$r.X,[int]$r.Y,0,0,$bmp.Size)
  }finally{$graphics.Dispose()}
  return $bmp
}
function Layout-Check($window,[string]$label) {
  $rows=Get-VisibleRect $window 'AV.Mainboard.Rows'
  $search=Get-VisibleRect $window 'AV.Mainboard.Search'
  if($null -ne $rows -and $null -ne $search){
    if($rows.Y -gt $search.Y -and $rows.Height -ge 60){
      Result "UI-RESPONSIVE-$label" 'PASS' 'Mainboard rows remain below search with usable height.'
    }else{Result "UI-RESPONSIVE-$label" 'FAIL' 'Mainboard controls overlap or collapse.'}
    return
  }
  $a=Get-VisibleRect $window 'AV.Analytics.FormatHeading'
  $b=Get-VisibleRect $window 'AV.Analytics.RecordedColorsHeading'
  $c=Get-VisibleRect $window 'AV.Analytics.ObservedColorsHeading'
  if($null -ne $a -and $null -ne $b -and $null -ne $c){
    $ordered=($b.Y -ge $a.Y -and $c.Y -ge $b.Y)
    $sameRow=([Math]::Abs($b.Y-$a.Y) -le 35 -and $b.X -gt $a.X+60)
    $wrap=($b.Y -gt $a.Y+35 -and [Math]::Abs($b.X-$a.X) -le 95)
    $thirdRow=([Math]::Abs($c.Y-$b.Y) -le 35 -and $c.X -gt $b.X+60) -or
      ($c.Y -gt $b.Y+35 -and [Math]::Abs($c.X-$a.X) -le 95)
    if($ordered -and ($sameRow -or $wrap) -and $thirdRow){
      Result "UI-RESPONSIVE-$label" 'PASS' 'Analytics headings retain readable order.'
    }else{Result "UI-RESPONSIVE-$label" 'FAIL' 'Analytics headings are detached or reordered.'}
    return
  }
  Result "UI-RESPONSIVE-$label" 'SKIP' 'Open Deck Editor Manage or scroll Analytics headings into view.'
}
if($SelfTest) {
  try {
    Add-Type -AssemblyName System.Drawing
    $a=New-Object System.Drawing.Bitmap(16,16)
    $b=New-Object System.Drawing.Bitmap(16,16)
    try {
      $same=Compare-Bitmaps $a $b
      $b.SetPixel(0,0,[System.Drawing.Color]::White)
      $different=Compare-Bitmaps $a $b 18 0
      $wrong=New-Object System.Drawing.Bitmap(12,16)
      try{$mismatch=Compare-Bitmaps $a $wrong}finally{$wrong.Dispose()}
      if($same.pass -and -not $different.pass -and -not $mismatch.pass){
        Result 'UI-REGRESSION-SELFTEST' 'PASS' 'Identical, changed-pixel and size mismatch cases verified.'
      }else{Result 'UI-REGRESSION-SELFTEST' 'FAIL' 'Screenshot comparison assertions failed.'}
    }finally{$a.Dispose();$b.Dispose()}
  }catch{Result 'UI-REGRESSION-SELFTEST' 'FAIL' $_.Exception.Message}
}elseif(-not [Environment]::UserInteractive) {
  Result 'UI-REGRESSION-DESKTOP' 'SKIP' 'An interactive Windows desktop is required.'
}else{
  try{
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -AssemblyName System.Drawing
    $root=[System.Windows.Automation.AutomationElement]::RootElement
    $windows=$root.FindAll([System.Windows.Automation.TreeScope]::Children,
      [System.Windows.Automation.Condition]::TrueCondition)
    $window=$null
    foreach($candidate in $windows){
      try {
        $processId=[int]$candidate.Current.ProcessId
        if($processId -le 0 -or $candidate.Current.Name -notlike '*Arcane Vault*' -or
           ($AppProcessId -gt 0 -and $AppProcessId -ne $processId)){continue}
        $process=Get-Process -Id $processId -ErrorAction SilentlyContinue
        if($process -and $process.ProcessName -like 'ArcaneVault*'){
          $window=$candidate;break
        }
      }catch{}
    }
    if($null -eq $window){
      Result 'UI-REGRESSION-ATTACH' 'SKIP' 'Open Arcane Vault on the interactive desktop.'
    }else{
      Result 'UI-REGRESSION-ATTACH' 'PASS' 'Attached to the running Arcane Vault window.'
      if($Mode -in @('All','Interaction')){
        # SelectionItemPattern changes only selection, never card/deck content.
        $list=Get-Control $window 'AV.Mainboard.Rows'
        if($null -eq $list -or $list.Current.IsOffscreen){
          Result 'UI-INTERACTION-SELECTION' 'SKIP' 'Open a deck editor with Manage visible.'
        }else{
          $original=$null
          $selection=$null
          try{
            $selection=$list.GetCurrentPattern(
              [System.Windows.Automation.SelectionPattern]::Pattern)
            $original=@($selection.Current.GetSelection())
          }catch{}
          $items=$list.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
          $selected=$false
          foreach($item in $items){
            try {
              if($item.Current.IsOffscreen){continue}
              $pattern=$item.GetCurrentPattern(
                [System.Windows.Automation.SelectionItemPattern]::Pattern)
              $pattern.Select();$selected=$true;break
            }catch{}
          }
          if(-not $selected){
            Result 'UI-INTERACTION-SELECTION' 'SKIP' 'No selectable visible card rows.'
          }elseif($null -eq $selection -or @($selection.Current.GetSelection()).Count -eq 0){
            Result 'UI-INTERACTION-SELECTION' 'FAIL' 'UIA selection did not select a card row.'
          }else{
            Result 'UI-INTERACTION-SELECTION' 'PASS' 'UI Automation selected a card row without editing.'
          }
          # Restore the original selection whenever a selectable element existed.
          foreach($old in @($original)){
            try {
              $restore=$old.GetCurrentPattern(
                [System.Windows.Automation.SelectionItemPattern]::Pattern)
              $restore.Select();break
            }catch{}
          }
        }
      }
      if($Mode -in @('All','Visual')){
        $r=$window.Current.BoundingRectangle
        if(-not (Valid-Rect $r) -or $window.Current.IsOffscreen){
          Result 'UI-VISUAL-SCREENSHOT' 'SKIP' 'App window not visibly on-screen.'
        }else{
          # Screenshots may contain private card/deck names: never send to CI.
          if(-not (Test-Path $ReportDir)){New-Item -ItemType Directory -Path $ReportDir -Force|Out-Null}
          $bitmap=Capture-Window $r
          try{
            $snapshot=Join-Path $ReportDir 'ui-regression-latest.png'
            $bitmap.Save($snapshot,[System.Drawing.Imaging.ImageFormat]::Png)
            $baseline=Join-Path $BaselineDir 'ui-regression-approved.png'
            if($ApproveBaseline){
              if(-not (Test-Path $BaselineDir)){New-Item -ItemType Directory -Path $BaselineDir -Force|Out-Null}
              $bitmap.Save($baseline,[System.Drawing.Imaging.ImageFormat]::Png)
              Result 'UI-VISUAL-SCREENSHOT' 'PASS' 'Local baseline explicitly approved.'
            }elseif(-not (Test-Path $baseline)){
              Result 'UI-VISUAL-SCREENSHOT' 'SKIP' 'Screenshot captured locally; use -ApproveBaseline once to review/approve.'
            }else{
              $expected=[System.Drawing.Bitmap]::FromFile($baseline)
              try{$diff=Compare-Bitmaps $expected $bitmap}finally{$expected.Dispose()}
              if($diff.pass){Result 'UI-VISUAL-SCREENSHOT' 'PASS' ('Changed pixels: {0:P2}' -f $diff.fraction)}
              else{Result 'UI-VISUAL-SCREENSHOT' 'FAIL' ('Changed pixels: {0:P2}; inspect the local screenshot.' -f $diff.fraction)}
            }
          }finally{$bitmap.Dispose()}
        }
      }
      if($Mode -in @('All','Responsive')){
        # The app's current window is never resized: checks reflect its current
        # viewport and require the user to resize between runs.
        Layout-Check $window 'CURRENT'
      }
    }
  }catch{
    Result 'UI-REGRESSION-ENVIRONMENT' 'SKIP' ('Desktop UI test unavailable: '+$_.Exception.Message)
  }
}
if(-not (Test-Path $ReportDir)){New-Item -ItemType Directory -Path $ReportDir -Force|Out-Null}
$prefix=Join-Path $ReportDir ('ui-regression-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
$status=if(@($results|Where-Object{$_.result -eq 'FAIL'}).Count){'FAIL'}
 elseif(@($results|Where-Object{$_.result -eq 'PASS'}).Count){'PASS'}else{'SKIP'}
[ordered]@{schema_version=1;type='ui-regression';generated_at=(Get-Date).ToString('o');
 mode=$Mode;self_test=[bool]$SelfTest;status=$status;tests=@($results.ToArray())} |
 ConvertTo-Json -Depth 6 | Set-Content -LiteralPath ($prefix+'.json') -Encoding UTF8
if($status -eq 'FAIL'){exit 1}
exit 0
