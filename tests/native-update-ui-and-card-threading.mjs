import assert from 'node:assert/strict';
import fs from 'node:fs';
const read=f=>fs.readFileSync(f,'utf8');
const details=read('ArcaneVault.Next.Desktop/NativeCardDetailsWindow.xaml.cs');
const window=read('ArcaneVault.Next.Desktop/MainWindow.xaml.cs');
const xaml=read('ArcaneVault.Next.Desktop/MainWindow.xaml');
const shortlist=read('ArcaneVault.Next.Desktop/NativeShortlistWorkspace.xaml.cs');
const prompt=read('ArcaneVault.Next.Desktop/MainWindow.NativeUpdatePrompt.cs');
const retention=read('ArcaneVault.Next.Collection/NativeVersionRetention.cs');
// CardName.Text is a WPF UI-thread property: never read it in background delegates.
for(const operation of [
  'GetPreferredArtwork(nativePath, cardName)',
  'NativeCardFaceLookup.Alternate(catalogPath,cardName,activeFrontArt)',
  'nativePath, cardName, !isSaved',
])assert.ok(details.includes(operation),'Captured UI value missing: '+operation);
assert.doesNotMatch(details,/Task\.Run\(\(\)\s*=>\s*NativeWorkspaceSafety\.GetPreferredArtwork\(nativePath,\s*CardName\.Text/);
assert.doesNotMatch(details,/Task\.Run\(\(\)\s*=>\s*NativeCardFaceLookup\.Alternate\(catalogPath,\s*CardName\.Text/);
assert.match(details,/string cardName = CardName\.Text/);
assert.match(window,/shortlistWorkspace=new NativeShortlistWorkspace/);
assert.match(xaml,/ShortlistWorkspaceHost/);
assert.match(shortlist,/public event Action\? ShortlistEdited/);
assert.match(prompt,/ConfirmNativeUpdateAsync/);
assert.match(window,/await ConfirmNativeUpdateAsync/);
assert.doesNotMatch(window.slice(window.indexOf('private async void OnCheckNativeUpdatesClicked'),window.indexOf('First-party native Scryfall sync')),/MessageBox\.Show|NativeInAppDialog\.Show/);
assert.match(retention,/CleanupAfterVerifiedLaunch/);
assert.match(xaml,/ActivityHistoryButton/);
// Deck Library cover and inspector must resolve the SAME saved commander
// printing, not show a generic card image beside a deck-specific tile.
const inspector=read('ArcaneVault.Next.Desktop/NativeCollectionWorkspace.DeckInspector.cs');
const library=read('ArcaneVault.Next.Desktop/NativeCollectionWorkspace.xaml.cs');
assert.match(library,/deck\.CommanderPrintingId\.Length > 0/);
assert.match(library,/NativePrintingArtwork\.Require\(catalogPath!,\s*deck\.Commander, printingId\)\.ImageUrl/);
assert.match(inspector,/selected\.IsCommander && deck\.CommanderPrintingId\.Length > 0/);
assert.match(inspector,/NativePrintingArtwork\.Require\(catalogPath!,\s*deck\.Commander, printingId\)\.ImageUrl/);
assert.match(inspector,/DeckInspectorArt\.ArtUrl = NativeCardArtwork\.IsTrustedArtworkUrl\(selectedArt\)/);
assert.match(inspector,/selectedArtworkUrl: chosenArtwork/);
console.log('PASS: WPF card details, unified commander printing in Library and Inspector, in-app update prompts and retention are wired');
