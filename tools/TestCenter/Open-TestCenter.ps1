#requires -Version 5.1
# Standalone, offline Windows GUI for Arcane Vault Test Center checks.
# The existing Test Center catalog is left intact. This launcher handles its
# native suites and the opt-in desktop regression runner.
[CmdletBinding()]
param([string]$SourceRoot='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$toolRoot=Split-Path -Parent $MyInvocation.MyCommand.Path
$defaultSource=Resolve-Path (Join-Path $toolRoot '..\..') -ErrorAction SilentlyContinue
if(-not $SourceRoot -and $defaultSource -and
   (Test-Path (Join-Path $defaultSource.Path 'ArcaneVault.Next.Tests\Program.cs'))){
  $SourceRoot=$defaultSource.Path
}
$reports=Join-Path $toolRoot 'Reports'
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$form=New-Object Windows.Forms.Form
$form.Text='Arcane Vault - Offline Test Center'
$form.StartPosition='CenterScreen'
$form.Size=New-Object Drawing.Size(830,655)
$form.MinimumSize=New-Object Drawing.Size(760,600)
$form.Font=New-Object Drawing.Font('Segoe UI',9)
$form.AutoScaleMode='Font'

function Label([string]$txt,[int]$x,[int]$y,[int]$w,[int]$h=25){
  $l=New-Object Windows.Forms.Label
  $l.Text=$txt;$l.Location=New-Object Drawing.Point($x,$y)
  $l.Size=New-Object Drawing.Size($w,$h)
  $form.Controls.Add($l)
  return $l
}
function Button([string]$txt,[int]$x,[int]$y,[int]$w){
  $b=New-Object Windows.Forms.Button
  $b.Text=$txt;$b.Location=New-Object Drawing.Point($x,$y)
  $b.Size=New-Object Drawing.Size($w,30)
  $form.Controls.Add($b)
  return $b
}
Label 'Source checkout (for native test suites):' 20 17 345 | Out-Null
$sourceBox=New-Object Windows.Forms.TextBox
$sourceBox.Location=New-Object Drawing.Point(20,43)
$sourceBox.Size=New-Object Drawing.Size(640,26)
$sourceBox.Anchor='Top,Left,Right'
$sourceBox.Text=$SourceRoot
$form.Controls.Add($sourceBox)
$browse=Button 'Browse' 670 41 120
$browse.Anchor='Top,Right'
$browse.Add_Click({
  $folder=New-Object Windows.Forms.FolderBrowserDialog
  $folder.Description='Choose an Arcane Vault source checkout'
  if(Test-Path -LiteralPath $sourceBox.Text){$folder.SelectedPath=$sourceBox.Text}
  if($folder.ShowDialog() -eq 'OK'){$sourceBox.Text=$folder.SelectedPath}
})
$tabs=New-Object Windows.Forms.TabControl
$tabs.Location=New-Object Drawing.Point(20,85)
$tabs.Size=New-Object Drawing.Size(770,350)
$tabs.Anchor='Top,Left,Right'
$form.Controls.Add($tabs)
$native=New-Object Windows.Forms.TabPage
$native.Text='Native regression suites'
$tabs.TabPages.Add($native)
$ui=New-Object Windows.Forms.TabPage
$ui.Text='Desktop UI checks'
$tabs.TabPages.Add($ui)
$coverage=New-Object Windows.Forms.TabPage
$coverage.Text='Setup and safety'
$tabs.TabPages.Add($coverage)

function TabLabel($tab,[string]$txt,[int]$y,[int]$h=26){
 $l=New-Object Windows.Forms.Label;$l.Text=$txt
 $l.Location=New-Object Drawing.Point(16,$y);$l.Size=New-Object Drawing.Size(710,$h)
 $tab.Controls.Add($l)
 return $l
}
TabLabel $native 'Choose a friendly test group. Native suites use disposable synthetic data.' 18 | Out-Null
$area=New-Object Windows.Forms.ComboBox
$area.DropDownStyle='DropDownList'
$area.Location=New-Object Drawing.Point(16,58)
$area.Size=New-Object Drawing.Size(430,28)
@(
  'DeckArtwork — Card artwork and deck covers',
  'MatchTracking — Match attribution and analytics',
  'DeckEditing — Deck save and edit safety',
  'DataSafety — Collection and backup integrity',
  'Everything — All named native suites'
)|ForEach-Object{[void]$area.Items.Add($_)}
$area.SelectedIndex=0
$native.Controls.Add($area)
$offline=New-Object Windows.Forms.CheckBox
$offline.Text='Offline (no package restore; requires cached dependencies)'
$offline.Location=New-Object Drawing.Point(16,107)
$offline.Size=New-Object Drawing.Size(590,27)
$native.Controls.Add($offline)
$runNative=New-Object Windows.Forms.Button
$runNative.Text='Run selected native checks'
$runNative.Location=New-Object Drawing.Point(16,161)
$runNative.Size=New-Object Drawing.Size(260,36)
$native.Controls.Add($runNative)
TabLabel $native 'Native test requirements: Windows + .NET 10 SDK and a full source checkout.' 218 48 | Out-Null

TabLabel $ui 'Connects to Arcane Vault already running on this Windows desktop.' 14 | Out-Null
$mode=New-Object Windows.Forms.ComboBox
$mode.DropDownStyle='DropDownList'
$mode.Location=New-Object Drawing.Point(16,51)
$mode.Size=New-Object Drawing.Size(450,27)
@('All','Interaction','Visual','Responsive') | ForEach-Object{[void]$mode.Items.Add($_)}
$mode.SelectedIndex=0
$ui.Controls.Add($mode)
TabLabel $ui 'Test category (Interaction selects a card; Visual compares screenshots).' 85 | Out-Null
$pidLabel=New-Object Windows.Forms.Label
$pidLabel.Text='Arcane Vault process ID (optional):'
$pidLabel.Location=New-Object Drawing.Point(16,123)
$pidLabel.Size=New-Object Drawing.Size(225,24)
$ui.Controls.Add($pidLabel)
$pidBox=New-Object Windows.Forms.NumericUpDown
$pidBox.Minimum=0;$pidBox.Maximum=2147483647
$pidBox.Location=New-Object Drawing.Point(240,120)
$pidBox.Size=New-Object Drawing.Size(130,26)
$ui.Controls.Add($pidBox)
$approve=New-Object Windows.Forms.CheckBox
$approve.Text='Approve new local screenshot baseline (replaces previous baseline)'
$approve.Location=New-Object Drawing.Point(16,158)
$approve.Size=New-Object Drawing.Size(660,27)
$ui.Controls.Add($approve)
$runUi=New-Object Windows.Forms.Button
$runUi.Text='Run desktop UI checks'
$runUi.Location=New-Object Drawing.Point(16,204)
$runUi.Size=New-Object Drawing.Size(245,34)
$ui.Controls.Add($runUi)
TabLabel $ui 'Resize manually, then rerun Responsive. Screenshots and reports stay local.' 250 43 | Out-Null

TabLabel $coverage 'Setup verifies the checkout and scripts. It does not access your collection.' 20 45 | Out-Null
$check=New-Object Windows.Forms.Button
$check.Text='Check setup'
$check.Location=New-Object Drawing.Point(16,75)
$check.Size=New-Object Drawing.Size(190,36)
$coverage.Controls.Add($check)
$testComparator=New-Object Windows.Forms.Button
$testComparator.Text='Test screenshot comparator'
$testComparator.Location=New-Object Drawing.Point(230,75)
$testComparator.Size=New-Object Drawing.Size(245,36)
$coverage.Controls.Add($testComparator)
TabLabel $coverage 'PASS means the check ran successfully. SKIP means prerequisites were not met.' 135 37 | Out-Null
TabLabel $coverage 'Screenshots may contain private card names. Never share a report without reviewing it.' 180 50 | Out-Null
$openReports=Button 'Open reports folder' 20 455 190
$status=Label 'Ready. Select a check to begin.' 225 462 550
$status.Anchor='Top,Left,Right'
$log=New-Object Windows.Forms.TextBox
$log.Multiline=$true;$log.ReadOnly=$true;$log.ScrollBars='Both'
$log.WordWrap=$false
$log.Location=New-Object Drawing.Point(20,505)
$log.Size=New-Object Drawing.Size(770,100)
$log.Anchor='Top,Bottom,Left,Right'
$form.Controls.Add($log)
$openReports.Add_Click({Start-Process explorer.exe -ArgumentList @($reports)})
function Write-Ui([string]$line){
  $log.AppendText($line+[Environment]::NewLine)
}
$state=[pscustomobject]@{Process=$null;Output='';Error='';RunLabel='';Started=$false}
$timer=New-Object Windows.Forms.Timer
$timer.Interval=450
$timer.Add_Tick({
  if($null -eq $state.Process){return}
  try{
    if(-not $state.Process.HasExited){return}
    $state.Process.Refresh()
    $code=$state.Process.ExitCode
    $timer.Stop()
    foreach($path in @($state.Output,$state.Error)){
      if(Test-Path -LiteralPath $path){
        Get-Content -LiteralPath $path -ErrorAction SilentlyContinue |
          Select-Object -Last 90 | ForEach-Object{Write-Ui $_}
      }
    }
    $status.Text=if($code -eq 0){"Finished (exit 0). Inspect PASS/SKIP in report."}else{"Failed (exit $code). Review report."}
    $state.Process.Dispose();$state.Process=$null
    $runNative.Enabled=$true;$runUi.Enabled=$true;$testComparator.Enabled=$true
  }catch{
    $timer.Stop();$status.Text='Check failed: '+$_.Exception.Message
    $runNative.Enabled=$true;$runUi.Enabled=$true;$testComparator.Enabled=$true
    $state.Process=$null
  }
})
function Begin-Test([string]$script,[string[]]$scriptArguments,[string]$label){
  if($null -ne $state.Process){return}
  if(-not (Test-Path -LiteralPath $script)){
    [Windows.Forms.MessageBox]::Show("Missing test script: $script",'Test Center')|Out-Null
    return
  }
  $stamp=(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)
  $stdout=Join-Path $reports ("gui-$stamp-out.log")
  $stderr=Join-Path $reports ("gui-$stamp-error.log")
  $powershell=(Get-Process -Id $PID).Path
  if(-not $powershell -or -not (Test-Path $powershell)){
    $powershell=Join-Path $PSHOME 'powershell.exe'
  }
  $args=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$script)+$scriptArguments
  # Start-Process joins ArgumentList entries: quote paths/arguments explicitly.
  # Escape PowerShell-quoted arguments using standard Windows command-line rules.
  $quoted=@($args | ForEach-Object {
    $v=[string]$_
    if($v -match '[\s"]'){
      $v='"'+($v -replace '(\\*)"', '$1$1\\"' -replace '(\\+) -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $state.Process=$process;$state.Output=$stdout;$state.Error=$stderr
    $state.RunLabel=$label
    $log.Clear()
    Write-Ui "Started: $label"
    Write-Ui "Reports: $reports"
    $status.Text="Running $label..."
    $runNative.Enabled=$false;$runUi.Enabled=$false;$testComparator.Enabled=$false
    $timer.Start()
  }catch{
    $status.Text='Launch failed: '+$_.Exception.Message
  }
}
$runNative.Add_Click({
  $root=$sourceBox.Text.Trim()
  $runner=Join-Path $root 'tools\Run-GuidedChecks.ps1'
  if(-not (Test-Path -LiteralPath (Join-Path $root 'ArcaneVault.Next.Tests\Program.cs'))){
    [Windows.Forms.MessageBox]::Show('Select a full Arcane Vault source checkout first.','Test Center')|Out-Null
    return
  }
  $areas=@('DeckArtwork','MatchTracking','DeckEditing','DataSafety','Everything')
  $args=@('-Area',$areas[$area.SelectedIndex])
  if($offline.Checked){$args+='-Offline'}
  Begin-Test $runner $args $areas[$area.SelectedIndex]
})
$runUi.Add_Click({
  if($approve.Checked){
    $answer=[Windows.Forms.MessageBox]::Show(
      'Overwrite the local screenshot baseline using the currently visible Arcane Vault window?',
      'Approve screenshot baseline','YesNo','Warning')
    if($answer -ne 'Yes'){return}
  }
  $script=Join-Path $toolRoot 'Run-UiRegression.ps1'
  $args=@('-Mode',[string]$mode.SelectedItem,'-AppProcessId',([string][int]$pidBox.Value),'-ReportDir',$reports)
  if($approve.Checked){$args+='-ApproveBaseline'}
  Begin-Test $script $args ('Desktop '+[string]$mode.SelectedItem)
})
$testComparator.Add_Click({
  Begin-Test (Join-Path $toolRoot 'Run-UiRegression.ps1') @('-SelfTest','-ReportDir',$reports) 'Screenshot self-test'
})
$check.Add_Click({
  $log.Clear()
  $root=$sourceBox.Text.Trim()
  $checks=[ordered]@{
    'Desktop regression script'=Test-Path -LiteralPath (Join-Path $toolRoot 'Run-UiRegression.ps1')
    'Native suite checkout'=Test-Path -LiteralPath (Join-Path $root 'ArcaneVault.Next.Tests\Program.cs')
    'Guided native launcher'=Test-Path -LiteralPath (Join-Path $root 'tools\Run-GuidedChecks.ps1')
    '.NET SDK available'=($null -ne (Get-Command dotnet -ErrorAction SilentlyContinue))
  }
  foreach($key in $checks.Keys){Write-Ui ("{0}: {1}" -f $(if($checks[$key]){'PASS'}else{'SKIP'}),$key)}
  $status.Text='Setup check completed. Missing items are prerequisites, not failed regressions.'
})
$form.Add_FormClosing({
  if($null -ne $state.Process -and -not $state.Process.HasExited){
    $decision=[Windows.Forms.MessageBox]::Show(
      'A check is still running. Closing the GUI will not stop that check. Close anyway?',
      'Test Center','YesNo','Question')
    if($decision -ne 'Yes'){$_.Cancel=$true}
  }
})
[void]$form.ShowDialog()
, '$1$1')+'"' 
    }
    $v
  })
  try{
    $process=Start-Process -FilePath $powershell -ArgumentList $quoted -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $state.Process=$process;$state.Output=$stdout;$state.Error=$stderr
    $state.RunLabel=$label
    $log.Clear()
    Write-Ui "Started: $label"
    Write-Ui "Reports: $reports"
    $status.Text="Running $label..."
    $runNative.Enabled=$false;$runUi.Enabled=$false;$testComparator.Enabled=$false
    $timer.Start()
  }catch{
    $status.Text='Launch failed: '+$_.Exception.Message
  }
}
$runNative.Add_Click({
  $root=$sourceBox.Text.Trim()
  $runner=Join-Path $root 'tools\Run-GuidedChecks.ps1'
  if(-not (Test-Path -LiteralPath (Join-Path $root 'ArcaneVault.Next.Tests\Program.cs'))){
    [Windows.Forms.MessageBox]::Show('Select a full Arcane Vault source checkout first.','Test Center')|Out-Null
    return
  }
  $areas=@('DeckArtwork','MatchTracking','DeckEditing','DataSafety','Everything')
  $args=@('-Area',$areas[$area.SelectedIndex])
  if($offline.Checked){$args+='-Offline'}
  Begin-Test $runner $args $areas[$area.SelectedIndex]
})
$runUi.Add_Click({
  if($approve.Checked){
    $answer=[Windows.Forms.MessageBox]::Show(
      'Overwrite the local screenshot baseline using the currently visible Arcane Vault window?',
      'Approve screenshot baseline','YesNo','Warning')
    if($answer -ne 'Yes'){return}
  }
  $script=Join-Path $toolRoot 'Run-UiRegression.ps1'
  $args=@('-Mode',[string]$mode.SelectedItem,'-AppProcessId',([string][int]$pidBox.Value),'-ReportDir',$reports)
  if($approve.Checked){$args+='-ApproveBaseline'}
  Begin-Test $script $args ('Desktop '+[string]$mode.SelectedItem)
})
$testComparator.Add_Click({
  Begin-Test (Join-Path $toolRoot 'Run-UiRegression.ps1') @('-SelfTest','-ReportDir',$reports) 'Screenshot self-test'
})
$check.Add_Click({
  $log.Clear()
  $root=$sourceBox.Text.Trim()
  $checks=[ordered]@{
    'Desktop regression script'=Test-Path -LiteralPath (Join-Path $toolRoot 'Run-UiRegression.ps1')
    'Native suite checkout'=Test-Path -LiteralPath (Join-Path $root 'ArcaneVault.Next.Tests\Program.cs')
    'Guided native launcher'=Test-Path -LiteralPath (Join-Path $root 'tools\Run-GuidedChecks.ps1')
    '.NET SDK available'=($null -ne (Get-Command dotnet -ErrorAction SilentlyContinue))
  }
  foreach($key in $checks.Keys){Write-Ui ("{0}: {1}" -f $(if($checks[$key]){'PASS'}else{'SKIP'}),$key)}
  $status.Text='Setup check completed. Missing items are prerequisites, not failed regressions.'
})
$form.Add_FormClosing({
  if($null -ne $state.Process -and -not $state.Process.HasExited){
    $decision=[Windows.Forms.MessageBox]::Show(
      'A check is still running. Closing the GUI will not stop that check. Close anyway?',
      'Test Center','YesNo','Question')
    if($decision -ne 'Yes'){$_.Cancel=$true}
  }
})
[void]$form.ShowDialog()
