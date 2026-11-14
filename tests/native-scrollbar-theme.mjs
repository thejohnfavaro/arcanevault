import assert from 'node:assert/strict';
import fs from 'node:fs';
const xaml=fs.readFileSync('ArcaneVault.Next.Desktop/App.xaml','utf8');
for(const label of ['VaultScrollThumb','VaultScrollPageButton',
  'VaultVerticalScrollBarTemplate','VaultHorizontalScrollBarTemplate',
  'PART_Track','PageUpCommand','PageDownCommand',
  'PageLeftCommand','PageRightCommand','IsDragging',
  '{DynamicResource VaultAccent}','{DynamicResource VaultAccentMuted}',
  '{x:Type ScrollBar}']) assert.ok(xaml.includes(label),'Missing themed scrollbar contract: '+label);
assert.match(xaml,/<Trigger Property="Orientation" Value="Horizontal">/);
assert.match(xaml,/<Setter Property="Height" Value="13"\/>/);
console.log('PASS: Windows WPF scrollbars share theme tokens, preserve native scroll tracks and style both orientations');
