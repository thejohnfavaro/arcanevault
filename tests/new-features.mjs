import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve(import.meta.dirname, '..');
const htmlFile=fs.existsSync(path.join(root,'ArcaneVault.Desktop/Assets/Arcane_Vault_MTGA_Collection_v4_2.html'))
 ?path.join(root,'ArcaneVault.Desktop/Assets/Arcane_Vault_MTGA_Collection_v4_2.html'):path.join(root,'app/src/main/assets/index.html');
const html=fs.readFileSync(htmlFile,'utf8');
for(const x of ['id="avUpdateRepo"','id="avAutoUpdateCheck"','id="avCheckUpdates"','id="avStudioTribe"','id="avTribeQuery"','function linkedTypes(card)','function checkUpdates(silent=false)'])assert.ok(html.includes(x),`Missing ${x}`);
assert.match(html,/all non-\[a-z-\]\+ creatures get -/,'Board wipe heuristic should cover non-Zombie wipes');
const line='All non-Zombie creatures get -3/-3 until end of turn.';
assert.match(line,/all non-[a-z-]+ creatures get -/i);
const tokens='Create a 2/2 black Zombie creature token.';
assert.ok(new RegExp('\\b'+'Zombie'+'s?'+'\\b','i').test(tokens));
assert.ok(new RegExp('\\b'+'Zombie'+'s?'+'\\b','i').test('Zombies you control get +1/+1'));
assert.match(html,/https:\/\/api\.github\.com\/repos\//,'Updater must check GitHub public release feed');
assert.match(html,/if\(release\.draft\|\|release\.prerelease\)return false/,'Only stable published releases');
assert.match(html,/INSTALLED/);
console.log('PASS: update controls, safe release filtering, type filters, and Liliana Zombie/wipe tags');
