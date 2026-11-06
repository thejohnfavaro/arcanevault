// Run with: node tests/regression.mjs
// These tests exercise pure calculation code from the shipped interface and inspect stable API contracts.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const project=path.resolve(here,'..');
const android=fs.existsSync(path.join(project,'app','src','main','assets','index.html'));
const html=fs.readFileSync(android?path.join(project,'app','src','main','assets','index.html'):path.join(project,'ArcaneVault.Desktop','Assets','Arcane_Vault_MTGA_Collection_v4_2.html'),'utf8');
assert.match(html, /Offline artwork cache/);
assert.match(html, /avArtCacheEnable/);
assert.match(html, /avArtCacheClear/);
assert.match(html, /\/api\/art\?url=/);
assert.match(html, /avCastSpell/);
assert.match(html, /avBottomCard/);
assert.match(html, /avLandChoice/);
assert.match(html, /unverifiedLegalityCount/);
assert.match(html, /function playLand/);
assert.match(html, /function mulliganHand/);
assert.match(html, /function castSpell/);
assert.match(html, /if\(!turn\|\|bottomDue\)/);
assert.match(html, /this\.dataset\.avArtFailed=orig/);
assert.match(html, /const required=Math\.ceil\(value\)/);
// Evaluate the real probability helpers from the embedded application, not duplicate formulae.
const start=html.indexOf('function choose(n,k)');
const end=html.indexOf('function roleCard(c)',start);
assert.ok(start>0 && end>start,'Probability code must exist');
const ctx=vm.createContext({});
vm.runInContext(html.slice(start,end)+';globalThis.exports={choose,atLeastLand,percentage};',ctx);
const {choose,atLeastLand,percentage}=ctx.exports;
assert.equal(choose(10,3),120);
assert.equal(choose(5,6),0);
assert.equal(atLeastLand(10,5,2,1), 1-20/90); // at least one of two drawn, 5 of 10 lands
assert.equal(atLeastLand(10,5,2,2),20/90);
assert.equal(atLeastLand(60,0,7,1),0);
assert.equal(atLeastLand(60,60,7,7),1);
assert.equal(atLeastLand(60,24,61,3),null);
assert.equal(percentage(0.5),'50.0%');
// Cross-platform cache API contracts must match exactly.
const service=android?fs.readFileSync(path.join(project,'app','src','main','java','com','arcanevault','mobile','MainActivity.java'),'utf8'):fs.readFileSync(path.join(project,'ArcaneVault.Desktop','LocalAppServer.cs'),'utf8');
for(const endpoint of ['/api/art','/api/art/status','/api/art/clear'])assert.ok(service.includes(endpoint),`Missing native endpoint: ${endpoint}`);
const cache=android?fs.readFileSync(path.join(project,'app','src','main','java','com','arcanevault','mobile','ArtworkCache.java'),'utf8'):fs.readFileSync(path.join(project,'ArcaneVault.Desktop','ArtworkCache.cs'),'utf8');
for(const host of ['cards.scryfall.io','svgs.scryfall.io'])assert.ok(cache.includes(host));
assert.match(cache,/200L \* 1024 \* 1024/);
assert.match(cache,/3_000_000/);
assert.ok(!cache.includes('api.scryfall.com'), 'Do not cache API responses as images');

// Exercise actual goldfish state functions in an isolated UI harness.
function extractFunction(name) {
 const index=html.indexOf('function '+name+'(');
 assert.ok(index>=0,'Missing '+name);
 const begin=html.indexOf('{',index);let depth=0,quote='',template=false,escaped=false;
 // The source has template literals; the extraction uses top-level function-to-function boundaries.
 const tail=html.slice(index);
 const next=tail.search(/\nfunction [a-zA-Z]/);
 assert.ok(next>0,'Missing terminator for '+name);
 return tail.slice(0,next);
}
const env=vm.createContext({
 Math,Number,Date,Set,Array, console,
 data:{avLandChoice:{value:'0'},avSpellChoice:{value:'0'},avBottomChoice:{value:'0'},avPlayDraw:{value:'play'}},
 id(k){return this.data[k];},
 sourceDeck(){return {entries:[{name:'Forest',qty:4},{name:'Island',qty:4},{name:'Test Spell',qty:1}]};},
 playableDeck(d){return d.entries.flatMap(e=>Array(e.qty).fill(e.name));},
 shuffle(a){return a;},
 isLand(n){return n==='Forest'||n==='Island';},
 collectionCard(n){return {meta:{mv:1}};},
 handShow(){},
 });
// Fake UI functions are bound by VM lexical context; use direct global names.
vm.runInContext(`
const data=globalThis.data;
const id=k=>data[k];
const sourceDeck=()=>({entries:[{name:'Forest',qty:4},{name:'Island',qty:4},{name:'Test Spell',qty:1}]});
const playableDeck=d=>d.entries.flatMap(e=>Array(e.qty).fill(e.name));
const shuffle=a=>a;
const isLand=n=>n==='Forest'||n==='Island';
const collectionCard=n=>({meta:{mv:1}});
const handShow=()=>{};
let currentHand=[],remaining=[],seen=0,turn=0,landsInPlay=[],playedLandThisTurn=false,handLog=[],mulligans=0,bottomDue=0,tappedLands=new Set(),castSpells=[];
${['newHand','mulliganHand','bottomCard','nextTurn','playLand','undoLand','castSpell'].map(extractFunction).join('\n')}
globalThis.testApi={newHand,mulliganHand,bottomCard,nextTurn,playLand,undoLand,castSpell,get:()=>({turn,bottomDue,hand:currentHand.slice(),lands:landsInPlay.slice(),tapped:tappedLands.size,cast:castSpells.slice(),log:handLog.slice()})};
`,env);
const g=env.testApi;
g.newHand();assert.equal(g.get().hand.length,7);assert.equal(g.get().bottomDue,0);
g.mulliganHand();assert.equal(g.get().bottomDue,1);g.nextTurn();assert.equal(g.get().turn,0,'Mulligan bottom must block turn 1');
g.bottomCard();assert.equal(g.get().bottomDue,0);assert.equal(g.get().hand.length,6);
g.nextTurn();assert.equal(g.get().turn,1);
g.playLand();assert.equal(g.get().lands.length,1);g.playLand();assert.equal(g.get().lands.length,1,'Only one land per turn');
g.nextTurn();g.playLand();assert.equal(g.get().lands.length,2);
console.log('PASS: simulated mulligan bottoming, blocked turn, and one-land-per-turn state transitions');

console.log('PASS: artwork cache endpoints, bounds, hosts, goldfish controls, legality diagnostics, and probability checks');
