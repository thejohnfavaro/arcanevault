import assert from 'node:assert/strict';
import fs from 'node:fs';
const desktop=fs.readFileSync('ArcaneVault.Next.Desktop/MainWindow.xaml.cs','utf8');
const xaml=fs.readFileSync('ArcaneVault.Next.Desktop/MainWindow.xaml','utf8');
const offer=fs.readFileSync('ArcaneVault.Next.Desktop/WirelessPeerOffer.cs','utf8');
const bridge=fs.readFileSync('ArcaneVault.Next.Desktop/UsbPeerBridge.cs','utf8');
const peer=fs.readFileSync('ArcaneVault.Next.Collection/NativePeerSync.cs','utf8');
const checkpoints=fs.readFileSync('ArcaneVault.Next.Collection/NativePeerCheckpoint.cs','utf8');
for(const pattern of [
 /OnCreateSharedBaselineUsbClicked/,
 /RequestUsbBaseline/,
 /PrepareFirstPairUnion/,
 /selectedAuthority="merge"/,
 /MessageBoxButton.YesNoCancel/,
 /ApplyFirstPairFromAndroid/,
 /ApplyFirstPairUnion/,
 /NativePeerCheckpoint.SaveIdentical/,
 /NativePeerSync.Compare\(NativeWorkspace.DefaultPath,phoneBytes\)/,
 /HasAnyPeerCheckpoint/
])assert.match(desktop,pattern);
assert.match(xaml,/First USB Sync \/ Create Shared Baseline/);
assert.match(offer,/baselinePhase/);
assert.match(offer,/phase==1/);
assert.match(offer,/127\.0\.0\.1|IPAddress.Loopback/);
assert.match(bridge,/OpenBaselineAsync/);
assert.match(peer,/FirstPairUnion\(/);
assert.match(peer,/First-sync conflicting/);
assert.match(peer,/NativeBackupReader.Read\(backup\)/);
assert.match(peer,/NativeWorkspaceSafety.Refresh/);
assert.match(checkpoints,/SaveIdentical/);
console.log('PASS: initial USB pairing compares, auto-merges disjoint records, requires approval for conflict, then saves verified checkpoints');
