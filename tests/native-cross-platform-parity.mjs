import assert from 'node:assert/strict';
import fs from 'node:fs';
const read=file=>fs.readFileSync(file,'utf8');
const catalog=read('ArcaneVault.Next.Desktop/CatalogSnapshot.cs');
const window=read('ArcaneVault.Next.Desktop/MainWindow.xaml.cs');
const markup=read('ArcaneVault.Next.Desktop/MainWindow.xaml');
const inspector=read('ArcaneVault.Next.Desktop/MainWindow.InspectorAndNotices.cs');
const commander=read('ArcaneVault.Next.Desktop/CommanderBrowserView.xaml.cs');
const commanderMarkup=read('ArcaneVault.Next.Desktop/CommanderBrowserView.xaml');
const details=read('ArcaneVault.Next.Desktop/NativeCardDetailsWindow.xaml.cs');
const lookup=read('ArcaneVault.Next.Desktop/NativeCardFaceLookup.cs');
for(const p of [
  /ManaValueMin/,/ManaValueMax/,/c\.mv >= \$mvMin/,/c\.mv <= \$mvMax/,
  /HasPrintingBackArtwork/,/BackImageUrl/,
])assert.match(catalog,p);
for(const p of [
  /ManaValueMinBox/,/ManaValueMaxBox/,/InspectorFlipButton/,
  /CardFaceFlip/,/OnCatalogTileFaceFlipClicked/,
])assert.match(markup+window,p);
for(const p of [
  /CatalogCacheKey/,/catalogPageCache/,/PrefetchCatalogPageArtwork/,
  /NativeCardArtwork\.LoadAsync/,/CardsList\.ScrollIntoView/,
  /InvalidateCatalogPageCache/,
])assert.match(window,p);
assert.match(inspector,/OnInspectorFlipClicked/);
assert.match(lookup,/image_back FROM variants/);
assert.match(lookup,/image=\$front/);
assert.match(commander,/FindCommanderAlternateFaceAsync/);
assert.match(commanderMarkup,/CommanderFaceFlip/);
assert.match(details,/OnArtworkFaceFlipClicked/);
console.log('PASS: Windows combined mana-value filters, bounded catalog caching, pagination and verified face flipping');
