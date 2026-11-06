import assert from 'node:assert/strict';
import vm from 'node:vm';
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
assert.match(html,/let INSTALLED=null;/,'Installed version must not be hardcoded');
assert.match(html,/fetch\('\/api\/app\/version'/,'Version must be read from the Windows host');
assert.match(html,/id="avInstalledVersion"/);
assert.match(html,/id="avLatestVersion"/);
assert.doesNotMatch(html,/Desktop Edition · Version [0-9]/,'About should not contain a stale version');
assert.match(html,/data-open="\$\{escapeHtml\(p\.add\.name\)\}"/,'Upgrade card details must open in-app');
const server=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/LocalAppServer.cs'),'utf8');
assert.match(server,/path == "\/api\/app\/version"/);
assert.match(server,/Assembly.GetName\(\).Version/);
console.log('PASS: update controls, safe release filtering, type filters, and Liliana Zombie/wipe tags');

// v5.1: recommendations must remain owned-only, color-verified, and explain their scoring.
assert.match(html,/id="studioRecommendStyle"/);
assert.match(html,/function studioSuggestions\(candidatePool=null,resultLimit=12\)/);
assert.match(html,/studioIdentityOK\(c\)===true&&studioLegalOK\(c\)!==false/);
assert.match(html,/Fills .*gap/);
assert.match(html,/Known .*kind/);
assert.match(html,/0 wildcards/);
assert.match(html,/id="studioRecommendSummary"/);
assert.match(html,/data-open="\$\{escapeHtml\(c.name\)\}">Details/);

// v5.2: catalog-backed, budget constrained nonowned suggestions.
for(const id of ['avFindCrafts','avAutoCraftPlan','avClearCraftPlan','avCraftBudget','avCraftResults'])assert.ok(html.includes('id="'+id+'"'));
assert.match(html,/function avCraftWithinBudget/);
assert.match(html,/scope:'unowned',arena:'arena'/);
assert.match(html,/card.meta\?\.arena!==true/);
assert.match(html,/Actual Arena craftability and wildcard rarity remain unverified/);

const craftSource=html.slice(html.indexOf('function avCraftBudget(){'),html.indexOf('function avCraftRender(){'));
assert.ok(craftSource.startsWith('function avCraftBudget(){') && craftSource.includes('function avCraftPlanOptimize(){'));
const craftContext={rarityIds:['common','uncommon','rare','mythic'],wc:{common:1,uncommon:0,rare:1,mythic:0},
 avCraftChosen:new Set(),avCraftCandidates:[
  {c:{name:'Owned-name clone A',meta:{rarity:'rare'}},score:20},
  {c:{name:'Owned-name clone B',meta:{rarity:'rare'}},score:18},
  {c:{name:'Common candidate',meta:{rarity:'common'}},score:9},
  {c:{name:'Mythic candidate',meta:{rarity:'mythic'}},score:5}],
 studioHas:()=>false,avCraftRender:()=>{}};
vm.createContext(craftContext);
vm.runInContext(craftSource,craftContext);
assert.equal(craftContext.avCraftCost({meta:{rarity:'rare'}}),'rare');
assert.equal(craftContext.avCraftCost({meta:{rarity:'special'}}),null);
craftContext.avCraftPlanOptimize();
assert.equal(craftContext.avCraftChosen.size,2,'Plan should not exceed the saved rarity budgets');
assert.equal(craftContext.avCraftChosen.has('owned-name clone a'),true);
assert.equal(craftContext.avCraftChosen.has('owned-name clone b'),false);
assert.equal(craftContext.avCraftChosen.has('common candidate'),true);
assert.equal(craftContext.avCraftWithinBudget('owned-name clone b'),false);
console.log('PASS: deterministic rarity budgeting and catalog recommendation contract');

// v5.3: case-insensitive acquisition and deck coverage changes.
assert.match(html,/function avCollectionDifference\(before,after\)/);
assert.match(html,/function avDeckCoverage\(deck,snapshot\)/);
assert.match(html,/function avCollectionImpact\(before,after\)/);
assert.match(html,/id="avCollectionDelta"/);
assert.match(html,/id="avLibraryCoverage"/);
assert.match(html,/id="avImportDelta"/);
assert.match(html,/renderLibrary\(\);renderPlanner\(\);renderStudio\(\)/);
const deltaStart=html.indexOf('function avCollectionDifference(before,after)');
const deltaEnd=html.indexOf('const AV_DELTA_KEY=',deltaStart);
assert.ok(deltaStart>0&&deltaEnd>deltaStart);
const deltaContext={basicLandNames:new Set(['Forest'])};
vm.createContext(deltaContext);
vm.runInContext(html.slice(deltaStart,deltaEnd)+';globalThis.exports={avCollectionDifference,avDeckCoverage,avCollectionImpact};',deltaContext);
const delta=deltaContext.exports;
const before={items:[['Lightning Bolt',1],['Forest',12],['Counterspell',2]]};
const after={items:[['lightning bolt',2],['Forest',11],['New Card',1]]};
const diff=delta.avCollectionDifference(before,after);
assert.equal(diff.newNames,1);assert.equal(diff.removedNames,1);
assert.equal(diff.copiesGained,2);assert.equal(diff.copiesLost,3);
const deck={id:'a',name:'Test Deck',commander:'Lightning Bolt',entries:[{name:'Counterspell',qty:1},{name:'New Card',qty:1},{name:'Forest',qty:30}]};
assert.equal(delta.avDeckCoverage(deck,before),1);
assert.equal(delta.avDeckCoverage(deck,after),1);
deltaContext.deckLibrary=[deck];
const changes=delta.avCollectionImpact(before,after);
assert.equal(changes.length,0);
console.log('PASS: collection deltas and saved-deck coverage');

// v5.4: explainable readiness analysis for any saved deck, no win-rate claims.
for(const id of ['avCompetitiveStyle','avCompetitiveRun','avCompetitiveResults'])assert.ok(html.includes('id="'+id+'"'));
assert.match(html,/function avCompetitiveAssess\(/);
assert.match(html,/function avCompetitiveUpgrades\(/);
assert.match(html,/Matchup pressure checkpoints/);
assert.match(html,/not a deck strength score/);
const competitionStart=html.indexOf('function avCompetitiveAssess(');
const competitionEnd=html.indexOf('function avCompetitiveStyle(',competitionStart);
assert.ok(competitionStart>0&&competitionEnd>competitionStart);
const competitionCtx={};
vm.createContext(competitionCtx);
vm.runInContext(html.slice(competitionStart,competitionEnd)+';globalThis.assess=avCompetitiveAssess;',competitionCtx);
const sampleCards=new Map([
 ['commander',{qty:1,meta:{colors:['B'],type:'Legendary Creature',mv:3,legalities:{historicbrawl:'legal'}}}],
 ['swamp',{qty:20,meta:{colors:[],type:'Basic Land',mv:0}}],
 ['answer',{qty:1,meta:{colors:['B'],type:'Instant',mv:2,legalities:{historicbrawl:'legal'}}}],
 ['forbidden',{qty:0,meta:{colors:['W'],type:'Sorcery',mv:3,legalities:{historicbrawl:'banned'}}}]
]);
const testDeck={name:'Sample',commander:'Commander',entries:[{name:'Swamp',qty:36},{name:'Answer',qty:2},{name:'Forbidden',qty:1}]};
const targets={midrange:{Ramp:[8,11],Removal:[8,14],'Card Draw':[8,14]}};
const results=competitionCtx.assess(testDeck,name=>sampleCards.get(String(name).toLowerCase()),c=>c.meta.type==='Instant'?['Removal']:[],targets,'midrange',new Set(['swamp']));
assert.equal(results.total,40);
assert.equal(results.lands,36);
assert.equal(results.missing.length,2);
assert.equal(results.duplicate.length,1);
assert.equal(results.offIdentity.length,1);
assert.equal(results.banned.length,1);
assert.equal(results.checks[0].pass,false);
assert.equal(results.checks[2].pass,false);
console.log('PASS: competitive assessment flags structure, ownership, singleton, identity and banned cards');

// v5.5: recorded match evidence is separate from heuristic deck analysis.
for(const id of ['avMatchInsightVersion','avMatchInsightFormat','avMatchInsightRefresh','avMatchInsightResults'])assert.ok(html.includes('id="'+id+'"'));
const evidenceStart=html.indexOf('function avMatchEvidence(');
const evidenceEnd=html.indexOf('let avInsightRequest=',evidenceStart);
assert.ok(evidenceStart>0&&evidenceEnd>evidenceStart);
const evidenceContext={};
vm.createContext(evidenceContext);
vm.runInContext(html.slice(evidenceStart,evidenceEnd)+';globalThis.analyze=avMatchEvidence;',evidenceContext);
const analyze=evidenceContext.analyze;
const matches=[
 {deckName:'My Deck',deckVersion:'v1',format:'Brawl',result:'win',playedUtc:'2026-01-02T00:00:00Z'},
 {deckName:'my deck',deckVersion:'v1',format:'Brawl',result:'loss',playedUtc:'2026-01-03T00:00:00Z'},
 {deckName:'My Deck',deckVersion:'v2',format:'Brawl',result:'win',playedUtc:'2026-01-04T00:00:00Z'},
 {deckName:'My Deck',deckVersion:'v1',format:'Standard',result:'loss',playedUtc:'2026-01-05T00:00:00Z'},
 {deckName:'My Deck',deckVersion:'v1',format:'Brawl',result:'draw',playedUtc:'2026-01-06T00:00:00Z'},
 {deckName:'My Deck',deckVersion:'v1',format:'Brawl',result:'unknown',playedUtc:'2026-01-07T00:00:00Z'},
 {deckName:'Other Deck',deckVersion:'v1',format:'Brawl',result:'loss',playedUtc:'2026-01-08T00:00:00Z'},
 {deckName:'',deckVersion:'',format:'Brawl',result:'loss',playedUtc:'2026-01-09T00:00:00Z'}];
const all=analyze(matches,'MY DECK');
assert.equal(all.total,4);assert.equal(all.wins,2);assert.equal(all.losses,2);assert.equal(all.draws,1);assert.equal(all.unknown,1);
assert.equal(all.unmatched,1);assert.equal(all.rate,0.5);assert.equal(all.recentTrend,null);
const selected=analyze(matches,'My Deck','v1','Brawl');
assert.equal(selected.total,2);assert.equal(selected.wins,1);assert.equal(selected.losses,1);
assert.equal(selected.draws,1);assert.equal(selected.unknown,1);
assert.equal(analyze(matches,'Nonexistent').total,0);
console.log('PASS: match evidence filters deck, version, queue and excludes draws/unknown results');

// v5.6 safe opt-in import and release contract.
for(const id of ['arenaDeckImports','arenaDeckPreview'])assert.ok(html.includes('id="'+id+'"'));
assert.match(html,/function arenaPreviewDeck\(index\)/);
assert.match(html,/function arenaImportResolved\(\)/);
assert.match(html,/arenaDeckId:String\(d\.arenaDeckId/);
assert.match(html,/total===60\|\|total===100/);
assert.match(html,/!unknown\.length/);

// v5.7: one-click library route and explicit batch review, no speculative imports.
for(const id of ['librarySyncArena','arenaBulkReview'])assert.ok(html.includes('id="'+id+'"'));
assert.match(html,/function arenaBulkStart\(\)/);
assert.match(html,/function arenaBulkCommit\(\)/);
assert.match(html,/function arenaBrawlCandidate\(d\)/);
assert.match(html,/function arenaApplyDraft\(draft\)/);
assert.match(html,/Previous versions will be kept/);
assert.match(html,/arenaDeckId===draft.source.deckId/);
const batchStart=html.indexOf('function arenaBrawlCandidate(d){');
const batchEnd=html.indexOf('async function arenaResolveDraft(',batchStart);
assert.ok(batchStart>0&&batchEnd>batchStart);
const bctx={};vm.createContext(bctx);
vm.runInContext(html.slice(batchStart,batchEnd)+';globalThis.exports={arenaBrawlCandidate,arenaDeckSame};',bctx);
const bulkFixtureDeck={name:'Test Brawl',commander:[{card:'123',qty:1}],main:[{card:'456',qty:99}]};
assert.equal(bctx.exports.arenaBrawlCandidate(bulkFixtureDeck),true);
assert.equal(bctx.exports.arenaBrawlCandidate({...bulkFixtureDeck,main:[{card:'456',qty:40}]}),false);
assert.equal(bctx.exports.arenaBrawlCandidate({...bulkFixtureDeck,commander:[]}),false);
assert.equal(bctx.exports.arenaBrawlCandidate({...bulkFixtureDeck,commander:[{card:'123',qty:2}]}),false);
assert.equal(bctx.exports.arenaDeckSame({name:'X',commander:'One',entries:[{name:'Swamp',qty:90},{name:'Bolt',qty:9}]},{name:'X',commander:'One',entries:[{name:'Bolt',qty:9},{name:'Swamp',qty:90}]}),true);
assert.equal(bctx.exports.arenaDeckSame({name:'X',commander:'One',entries:[{name:'Swamp',qty:99}]},{name:'X',commander:'One',entries:[{name:'Swamp',qty:98}]}),false);
console.log('PASS: batch Arena Brawl eligibility and unchanged deck comparisons');

// v5.8: matched decks can use the existing reviewed Arena deck import route.
for(const id of ['perfImportStatus','data-perf-import'])assert.ok(html.includes(id));
for(const fn of ['function perfImportPayload(match)','async function perfImportMatch(matchId)','function perfImportButton(match)'])assert.ok(html.includes(fn));
assert.match(html,/const draft=await arenaResolveDraft\(payload,serial\)/);
assert.match(html,/const outcome=arenaApplyDraft\(draft\)/);
assert.match(html,/candidates.length===1\?candidates\[0\]:null/);
console.log('PASS: Match Performance reuses review, ID matching, and revision-safe deck import');

// v5.9: Studio saves directly to the linked library deck, including renamed decks.
assert.match(html,/id="studioSaveLibrary"/);
assert.match(html,/id="studioLibrarySaveStatus"/);
assert.match(html,/\$\('studioSaveLibrary'\)\.onclick=libraryStore/);
const saveStart=html.indexOf('function libraryStore(){');
const saveEnd=html.indexOf('function libraryLoad(id)',saveStart);
assert.ok(saveStart>0&&saveEnd>saveStart);
const saveCode=html.slice(saveStart,saveEnd);
for(const marker of ['const active=activeDeckId?deckLibrary.find','let existing=active||deckLibrary.find','librarySnapshot(existing','activeDeckId=existing.id','saveLibrary();renderLibrary();renderPlanner()'])assert.ok(saveCode.includes(marker),marker);
assert.doesNotMatch(saveCode,/if\(existing&&existing\.name!==name\)existing=null/);
assert.match(html,/studio=\{name:'New Brawl Deck',commander:'',entries:\[\]\};activeDeckId=null/);
console.log('PASS: Deck Studio direct save, rename continuity and new-draft unlinking');

// v5.9.1: technical match-result provenance is displayed as plain language.
assert.match(html,/function perfResultSourceLabel\(source\)/);
assert.match(html,/arena_match_result:'Arena verified'/);
assert.match(html,/manual:'Manually entered'/);
assert.match(html,/unverified:'Unverified'/);
assert.match(html,/escapeHtml\(perfResultSourceLabel\(m\.resultSource\)\)/);
console.log('PASS: human-readable match result provenance labels');

// v5.9.2: ownership status is explicitly CSV-based; importing from Arena Connection uses the same reviewed collection replacement.
for(const id of ['arenaRefreshCollection','arenaCollectionCsv','arenaCollectionFreshness'])assert.ok(html.includes('id="'+id+'"'));
assert.match(html,/Arena log monitoring does not synchronize card ownership/);
assert.match(html,/function arenaCollectionFreshness\(\)/);
assert.match(html,/await importCsv\(file\)/);
assert.match(html,/save\(AV_OWNERSHIP_IMPORT_KEY,\{at:new Date\(\)\.toISOString\(\),name:file.name\}\)/);
assert.match(html,/Last successful CSV refresh:/);
console.log('PASS: collection freshness and explicit reviewed CSV refresh pathway');

// v5.10: explicit Arena-grant deltas only; no guesses from match or deck events.
for(const id of ['arenaGrantAuto','arenaGrantStatus'])assert.ok(html.includes('id="'+id+'"'));
assert.match(html,/async function arenaApplyLoggedGrants\(snapshot\)/);
assert.match(html,/arenaGrantState\.enabled/);
assert.match(html,/arenaGrantRecord\(event\.id\)/);
assert.match(html,/await fetch\('\/api\/arena\/resolve\?'/);
assert.match(html,/save\('cards',cards\.map/);
assert.match(html,/for\(const grant of arenaLastSnapshot\?\.cardGrants\|\|\[\]\)arenaGrantRecord\(grant\.id\)/);
const grantBackend=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/ArenaLogConnector.cs'),'utf8');
for(const marker of ['CaptureCardGrants(inv)','ReadGrantCards(JsonElement cards)','"GrantedCards", "grantedCards"','SHA256.HashData','arena-card-grants.json','cardGrants = cardGrants.Values'])assert.ok(grantBackend.includes(marker),marker);
assert.doesNotMatch(grantBackend,/Process\.GetProcessesByName|ReadProcessMemory|refresh_token/);
console.log('PASS: explicit grant capture, local replay ledger, opt-in and baseline protections');

// v5.11: match deck correction uses only canonical names from saved Deck Library.
for(const marker of ['function perfSavedDeckOptions(){','function perfMatchedSavedDeck(match,options=perfSavedDeckOptions())','function perfDeckDropdown(match,options)','async function perfSaveAssignedDeck(matchId)','data-match-deck','data-match-assign'])assert.ok(html.includes(marker),marker);
assert.doesNotMatch(html,/prompt\('Deck name \(empty means unassigned\)'/);
assert.match(html,/await perfApi\('\/api\/matches\/correct',\{id:match\.matchId,deck:next\}\)/);
const optionsBegin=html.indexOf('function perfSavedDeckOptions(){');
const optionsEnd=html.indexOf('async function perfSaveAssignedDeck(',optionsBegin);
assert.ok(optionsBegin>0 && optionsEnd>optionsBegin);
const dropdownSource=html.slice(optionsBegin,optionsEnd);
assert.match(dropdownSource,/const options=\[\],seen=new Set\(\)/);
assert.match(dropdownSource,/if\(!name\|\|seen\.has\(key\)\)continue/);
assert.match(dropdownSource,/arenaDeckId\.toLowerCase\(\)===arenaId\.toLowerCase\(\)/);
assert.match(dropdownSource,/Recorded: /);
const matchesBackend=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/MatchHistory.cs'),'utf8');
assert.match(matchesBackend,/r\.deckName\.Trim\(\)\.ToUpperInvariant\(\)/);
assert.match(matchesBackend,/WHEN \$name IS NOT NULL THEN 'Deck assignment: manual'/);
console.log('PASS: canonical saved-deck assignments, manual provenance and case-insensitive grouping');

// v5.12: conservative minimum ownership evidence from an exact played Arena deck.
for(const id of ['arenaPlayedDeckRecover','arenaPlayedDeckAuto','arenaPlayedDeckStatus'])assert.ok(html.includes('id="'+id+'"'));
for(const marker of ['function arenaPlayedDeckCandidates(matches,decks)','function arenaPlayedDeckMissing(draft)','async function arenaRecoverPlayedDeckCards()','Inferred from played Arena deck (minimum 1 copy)','if(arenaPlayedDeckAuto&&d.enabled)arenaRecoverPlayedDeckCards()'])assert.ok(html.includes(marker),marker);
const playedStart=html.indexOf('function arenaPlayedDeckCandidates(matches,decks)');
const playedEnd=html.indexOf('async function arenaRecoverPlayedDeckCards()',playedStart);
assert.ok(playedStart>0&&playedEnd>playedStart);
const playableCtx={
 arenaBrawlCandidate:d=>d.commander?.length===1&&d.main?.length===1&&(d.commander[0].qty+d.main[0].qty===100),
 index:new Map([['owned',{qty:2}],['missing',{qty:0}]])
};
vm.createContext(playableCtx);
vm.runInContext(html.slice(playedStart,playedEnd)+';globalThis.match=arenaPlayedDeckCandidates;globalThis.missing=arenaPlayedDeckMissing;',playableCtx);
const payload={deckId:'arena123',name:'My Brawl',commander:[{card:'11',qty:1}],main:[{card:'22',qty:99}]};
assert.equal(playableCtx.match([{deckId:'arena123',result:'win'}],[payload]).length,1);
assert.equal(playableCtx.match([{deckId:'arena123',result:'unknown'}],[payload]).length,0);
assert.equal(playableCtx.match([{deckId:'other',result:'win'}],[payload]).length,0);
assert.equal(playableCtx.match([{deckId:'',deckName:'My Brawl',result:'win'}],[payload]).length,0);
assert.equal(playableCtx.missing({commander:'Owned',entries:[{name:'Missing',qty:4},{name:'Missing',qty:1},{name:'Fresh',qty:99}]}).length,2);
assert.match(html,/if\(Number\(index\.get\(key\)\?\.qty\)>0\)continue/);
assert.match(html,/card\.qty=1/);
console.log('PASS: exact played-deck ID evidence, minimum one-copy inference and no duplicates');

// v5.13: independent physical collection with CSV import and manual quantity tracking.
for(const id of ['physical','physicalCsv','physicalImportMode','physicalAdd','physicalRows','physicalExport','physicalSummary'])assert.ok(html.includes('id="'+id+'"'),id);
for(const token of ['const PHYSICAL_KEY=','function physicalNormalize(rows)','function physicalCsvRows(raw)','function physicalRender()','function physicalInit()','physicalCards,wildcards:wc','physicalCards=physicalNormalize(data.physicalCards)'])assert.ok(html.includes(token),token);
assert.match(html,/if\(tab==='physical'\)physicalRender\(\)/);
assert.match(html,/studioInit\(\);libraryInit\(\);physicalInit\(\);plannerInit\(\)/);
const physicalStart=html.indexOf('const PHYSICAL_KEY='),physicalEnd=html.indexOf('// Arcane Vault v4: deck library',physicalStart);
assert.ok(physicalStart>0&&physicalEnd>physicalStart);
const physicalSnippet=html.slice(physicalStart,physicalEnd);
assert.doesNotMatch(physicalSnippet,/cards\s*=\s*physicalCards|save\('cards',physicalCards\)/);
const normalizeStart=html.indexOf('const physicalKey='),normalizeEnd=html.indexOf('function physicalSave()',normalizeStart);
const normalizedContext={};vm.createContext(normalizedContext);
vm.runInContext(html.slice(normalizeStart,normalizeEnd)+';globalThis.normalize=physicalNormalize;',normalizedContext);
const physicalInput=[{name:'Sol Ring',qty:1,set:'CMM',number:'1',finish:'foil'},{name:'sol ring',qty:2,set:'CMM',number:'1',finish:'foil'},{name:'Sol Ring',qty:4,set:'CMM',number:'1',finish:'nonfoil'},{name:'Invalid',qty:-1}];
const normalized=normalizedContext.normalize(physicalInput);
assert.equal(normalized.length,2);
assert.equal(normalized.find(c=>c.finish==='foil').qty,3);
assert.equal(normalized.find(c=>c.finish==='nonfoil').qty,4);
const parseStart=html.indexOf('function parseCsv('),parseEnd=html.indexOf('async function importCsv(',parseStart);
const physicalCsvStart=html.indexOf('function physicalCsvRows(raw)'),physicalCsvEnd=html.indexOf('function physicalInit()',physicalCsvStart);
const csvContext={};vm.createContext(csvContext);
vm.runInContext(html.slice(parseStart,parseEnd)+html.slice(normalizeStart,normalizeEnd)+html.slice(physicalCsvStart,physicalCsvEnd)+';globalThis.read=physicalCsvRows;',csvContext);
const read=csvContext.read('Card Name,Quantity,Edition,Collector Number,Foil\nSol Ring,2,CMM,1,true\nSol Ring,3,CMM,1,false');
assert.equal(read.entries.length,2);
assert.equal(read.entries.find(c=>c.finish==='foil').qty,2);
assert.equal(read.entries.find(c=>c.finish==='nonfoil').qty,3);
console.log('PASS: physical inventory isolated from Arena, printings, CSV parsing, edits and backups');
