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
for(const token of ['const PHYSICAL_KEY=','function physicalNormalize(rows)','function physicalCsvRows(raw)','function physicalRender()','function physicalInit()','physicalCards,preferredArtwork,wildcards:wc','physicalCards=physicalNormalize(data.physicalCards)'])assert.ok(html.includes(token),token);
assert.match(html,/if\(tab==='physical'\)physicalRender\(\)/);
assert.match(html,/studioInit\(\);libraryInit\(\);physicalInit\(\);portableSyncInit\(\);plannerInit\(\)/);
const physicalStart=html.indexOf('const PHYSICAL_KEY='),physicalEnd=html.indexOf('// Arcane Vault v4: deck library',physicalStart);
assert.ok(physicalStart>0&&physicalEnd>physicalStart);
const physicalSnippet=html.slice(physicalStart,physicalEnd);
assert.doesNotMatch(physicalSnippet,/cards\s*=\s*physicalCards|save\('cards',physicalCards\)/);
const normalizeStart=html.indexOf('const physicalKey='),normalizeEnd=html.indexOf('let physicalCards=physicalNormalize(',normalizeStart);
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

// v5.14: auto-detect common physical collection CSV exports, reject malformed quantities,
// and preserve original printing and inventory attributes.
for(const marker of ["source='ManaBox'","source='Moxfield'","source='Archidekt'","source='Delver Lens'","source='TCGplayer-style'","source='Deckbox-style'","id=\"physicalImportProfile\"","skippedReasons","scryfallId","purchasePrice"])assert.ok(html.includes(marker),marker);
const mapStart=html.indexOf('const physicalKey='),mapEnd=html.indexOf('let physicalCards=physicalNormalize(',mapStart);
const csvBegin=html.indexOf('function physicalCsvRows(raw)'),csvFinish=html.indexOf('function physicalInit()',csvBegin);
const csvParserBegin=html.indexOf('function parseCsv('),csvParserEnd=html.indexOf('async function importCsv(',csvParserBegin);
const importsContext={};vm.createContext(importsContext);
vm.runInContext(html.slice(csvParserBegin,csvParserEnd)+html.slice(mapStart,mapEnd)+html.slice(csvBegin,csvFinish)+';globalThis.read=physicalCsvRows;',importsContext);
const manabox=importsContext.read('Binder Name,Binder Type,Name,Set code,Collector number,Foil,Quantity,Scryfall ID,Condition,Language\nMain,Binder,Sol Ring,cmm,1,etched,2,uuid,NM,English\nWishlist,List,Black Lotus,lea,1,normal,1,uuid,NM,English');
const mythic=importsContext.read('Card Name,Set Code,Set Name,Collector Number,Rarity,Language,Quantity,Condition,Finish,Scryfall ID,Container Type,Container Name\nSol Ring,cmm,Commander Masters,1,uncommon,en,2,NM,nonfoil,abc,box,bulk');
assert.equal(mythic.source,'Mythic Tools');
assert.equal(mythic.entries[0].location,'bulk');
assert.equal(mythic.entries[0].scryfallId,'abc');
assert.equal(manabox.source,'ManaBox');
assert.equal(manabox.entries.length,1);
assert.equal(manabox.entries[0].qty,2);
assert.equal(manabox.entries[0].finish,'etched');
assert.equal(manabox.entries[0].location,'Main');
assert.equal(manabox.skipped,1);
const moxfield=importsContext.read('Count,Name,Edition,Collector Number,Foil\n1,Sol Ring,CMM,1,foil');
assert.equal(moxfield.source,'Moxfield');
assert.equal(moxfield.entries[0].finish,'foil');
const archidekt=importsContext.read('Quantity,Name,Finish,Condition,Date Added,Language,Purchase Price,Edition Name,Edition Code,Scryfall ID,Collector Number\n4,Sol Ring,Foil,NM,2026-01-01,English,2,Commander Masters,CMM,abc,7');
assert.equal(archidekt.source,'Archidekt');
assert.equal(archidekt.entries[0].set,'CMM');
assert.equal(archidekt.entries[0].scryfallId,'abc');
assert.equal(archidekt.entries[0].condition,'NM');
assert.throws(()=>importsContext.read('Name,Quantity\nSol Ring,-1'),/No positive-quantity/);
assert.throws(()=>importsContext.read('Name\nSol Ring'),/No quantity/);
console.log('PASS: physical CSV format recognition, printing attributes, wishlist exclusion and invalid quantity safety');

// v5.15: curated release notes in GitHub Releases and complete, safe in-app presentation.
const releaseWorkflow=fs.readFileSync(path.join(root,'.github/workflows/windows.yml'),'utf8');
const releaseNotes=fs.readFileSync(path.join(root,'releases/windows-v5.15.0.md'),'utf8');
assert.match(releaseWorkflow,/gh release create .* --notes-file \$notes/);
assert.doesNotMatch(releaseWorkflow,/--generate-notes/);
assert.match(releaseWorkflow,/Test-Path \$notes/);
for(const section of ['Added','Changed','Fixed','Removed'])assert.ok(releaseNotes.includes('## '+section),section);
assert.ok(releaseNotes.length>900,'Release notes should exceed the previous truncation length');
for(const marker of ['function renderReleaseNotes(parent,markdown)','function releaseInline(element,value)','renderReleaseNotes(result,found.release.body)','const updateAvailable=semverCompare(found.version,INSTALLED)>0','av-release-notes'])assert.ok(html.includes(marker),marker);
assert.doesNotMatch(html,/found\.release\.body[^;\n]*slice\(0,900\)/);
assert.match(html,/document\.createTextNode\(part\)/);
assert.match(html,/if\(updateAvailable&&repo==='thejohnfavaro\/arcanevault'\)result\.append\(install\)/);
console.log('PASS: curated full changelogs published and safely rendered without truncation');

// v5.16: two distinct ownership pools and full artwork in Goldfishing.
assert.match(html,/<label>Availability <select id="arenaScope"/);
for(const name of ['All Magic Cards','Arena Only','Physical Only'])assert.ok(html.includes('>'+name+'</option>'),name);
for(const marker of ['function physicalOwnedCount(name)','function ownershipBySource(name)','function ownedCollectionCards(physicalOnly=false)','function ownershipCountsHtml(name)','Arena owned:','Physical owned:','Arena owned ×','Physical owned ×'])assert.ok(html.includes(marker),marker);
assert.match(html,/if\(arenaScope==='physical'\|\|\(arenaScope==='any'&&catalogScope==='owned'\)\)/);
assert.match(html,/arena:arenaScope==='arena'\?'arena':'any'/);
assert.match(html,/if\(tab==='physical'\)physicalRender\(\)/);
assert.match(html,/class="av-hand-card"><img loading="lazy" decoding="async"/);
assert.match(html,/class="av-hand-fallback"/);
assert.match(html,/function collectionCard\(name\)\{return index\.get\(.+\)\|\|catalogIndex\.get/);
assert.doesNotMatch(html,/class="av-hand-card"><b>\$\{esc\(x\)\}<\/b>/);
assert.match(html,/arenaOwnedCount|ownershipBySource\(c\.name\)\.arena/);
const notes516=fs.readFileSync(path.join(root,'releases/windows-v5.16.0.md'),'utf8');
for(const section of ['Added','Changed','Fixed','Removed'])assert.ok(notes516.includes('## '+section));
assert.match(notes516,/Goldfishing/);
assert.match(notes516,/Physical Only/);
console.log('PASS: Card Browser dual ownership, availability options and Goldfishing artwork');


// Set Progress ownership sources must remain independent while retaining the same set/catalog filters.
for(const marker of ['data-set-source="arena"','data-set-source="physical"',"setCollectionSourceV1",'function setPhysicalInventory()','/api/catalog/sets/physical?','/api/catalog/set-cards/physical?'])
  assert.ok(html.includes(marker),'Missing Set Progress source integration: '+marker);
assert.match(html,/setCollectionSource==='physical'\?'Physical':'Arena'/);
assert.match(html,/setCardRequest\+\+/);
const sourceStart=html.indexOf('function setPhysicalInventory()');
const sourceEnd=html.indexOf('function setSourceUI()',sourceStart);
const physicalScope={physicalCards:[{name:'Sol Ring',qty:2,set:'CMM'},{name:'sol ring',qty:3,set:'C21'},{name:'Opt',qty:1,set:'XLN'},{name:'Invalid',qty:0}]};
vm.createContext(physicalScope);
vm.runInContext(html.slice(sourceStart,sourceEnd)+';globalThis.inventory=setPhysicalInventory;',physicalScope);
const physicalOutput=physicalScope.inventory();
assert.equal(physicalOutput.length,2);
assert.equal(physicalOutput.find(c=>c.name==='Sol Ring').qty,5);
assert.equal(physicalOutput.find(c=>c.name==='Opt').qty,1);
const serverSource=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/LocalAppServer.cs'),'utf8');
const catalogSource=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/ScryfallCatalog.cs'),'utf8');
for(const marker of ['/api/catalog/sets/physical','/api/catalog/set-cards/physical','PreparePhysicalOwnership(db, physicalCards)','physical_ownership','CREATE TEMP TABLE physical_ownership'])
  assert.ok((serverSource+catalogSource).includes(marker),'Missing physical catalog implementation: '+marker);
assert.match(catalogSource,/physicalCards\.HasValue \? "physical_ownership" : "ownership"/);
assert.match(catalogSource,/DROP TABLE IF EXISTS temp\.physical_ownership; CREATE TEMP TABLE physical_ownership/,
 'Repeated physical set requests must reset stale pooled-connection temporary ownership before inserting fresh copies');
console.log('PASS: Arena/Physical Set Progress toggle, physical quantity grouping, isolated catalog query and card paging');


// v5.18: paper-only valuation from exact printing identifiers, never an arbitrary name match.
for(const marker of ['id="physicalValuePanel"','id="physicalValueRefresh"','id="physicalValueTotal"','id="physicalValuePriced"','id="physicalValueUnpriced"','id="physicalMetadataRepair"','function physicalRefreshValues()','function physicalPriceIdentifier(c)','function physicalCardQuote(raw,finish)','function physicalRepairMetadata()','physicalValueRender();'])
  assert.ok(html.includes(marker),'Missing physical valuation feature: '+marker);
const pricingStart=html.indexOf('const priceNumber=');
const pricingEnd=html.indexOf('function physicalValueRender()',pricingStart);
assert.ok(pricingStart>0&&pricingEnd>pricingStart);
const pricingContext={physicalValues:null,physicalKey:()=>''};
vm.createContext(pricingContext);
vm.runInContext(html.slice(pricingStart,pricingEnd)+
 ';globalThis.priceId=physicalPriceIdentifier;globalThis.quote=physicalCardQuote;globalThis.usd=priceNumber;',pricingContext);
const priceCard={name:'Sol Ring',set:'CMM',number:'123',finish:'foil'};
assert.equal(pricingContext.priceId(priceCard).set,'cmm');
assert.equal(pricingContext.priceId(priceCard).collector_number,'123');
assert.equal(pricingContext.priceId({name:'Sol Ring',set:'',number:''}),null);
assert.equal(pricingContext.priceId({name:'Arena Exclusive',set:'',number:''}),null);
const print={digital:false,games:['paper'],prices:{usd:'1.00',usd_foil:'3.50',usd_etched:null}};
assert.equal(pricingContext.quote(print,'nonfoil'),1);
assert.equal(pricingContext.quote(print,'foil'),3.5);
assert.equal(pricingContext.quote(print,'etched'),null);
assert.equal(pricingContext.quote({...print,digital:true},'foil'),null);
assert.equal(pricingContext.quote({...print,games:['arena']},'nonfoil'),null);
assert.equal(pricingContext.usd(null),null);
assert.match(html,/const c=index\.get\(key\)\|\|catalogIndex\.get\(key\)\|\|ownedCollectionCards\(true\)/);
const proxy=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/LocalAppServer.cs'),'utf8');
for(const token of ['Guid.TryParse(id','Get("collector_number")','Get("set")','Get("name")'])
 assert.ok(proxy.includes(token),'Missing validated exact printing identifier: '+token);
console.log('PASS: physical paper-only print valuation, source labels, unavailable finish prices, and physical-only metadata repair');


// v5.19: physical inventory set release dates, paper printing selection, move-vs-add quantity safety.
const catalogSrc=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/ScryfallCatalog.cs'),'utf8');
const localSrc=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/LocalAppServer.cs'),'utf8');
for(const tag of ['id="physicalSort"','value="release-desc"','value="release-asc"',
 'function physicalSortCards(a,b,mode=physicalSort)','function physicalLoadSetDates()',
 'physicalSetDatesLoaded','id="physicalPrintingEditor"','id="physicalPrintingSelect"',
 'id="physicalPrintingAssign"','id="physicalPrintingAdd"','data-physical-print','data-manage-physical',
 'function physicalOpenPrintingPicker(name,sourceKey=null)','function physicalCommitPrinting(mode)',
 'function physicalPrintingsHtml(name)'])
 assert.ok(html.includes(tag),'Physical inventory release sorting or print selector missing '+tag);
assert.ok(localSrc.includes('/api/catalog/set-release-dates'));
assert.ok(catalogSrc.includes('public object SetReleaseDates()'));
assert.ok(catalogSrc.includes("MIN(release) AS released"));
assert.ok(catalogSrc.includes("$scope!='paper'"));
const sortStart=html.indexOf('const PHYSICAL_SORT_KEY=');
const sortEnd=html.indexOf('async function physicalLoadSetDates()',sortStart);
const sortCtx={load:()=> 'name',physicalSetDates:{}};
vm.createContext(sortCtx);
vm.runInContext(html.slice(sortStart,sortEnd)+';globalThis.sortCards=physicalSortCards;',sortCtx);
vm.runInContext("physicalSetDates={new:'2026-11-01',old:'2025-01-12'};",sortCtx);
const a=[{name:'Alpha',set:'old',number:'1',finish:'nonfoil'},
 {name:'Beta',set:'new',number:'1',finish:'nonfoil'},
 {name:'Unknown',set:'unk',number:'1',finish:'nonfoil'}];
assert.deepEqual(a.slice().sort((x,y)=>sortCtx.sortCards(x,y,'release-desc')).map(c=>c.name),
 ['Beta','Alpha','Unknown']);
assert.deepEqual(a.slice().sort((x,y)=>sortCtx.sortCards(x,y,'release-asc')).map(c=>c.name),
 ['Alpha','Beta','Unknown']);
assert.deepEqual(a.slice().sort((x,y)=>sortCtx.sortCards(x,y,'name')).map(c=>c.name),
 ['Alpha','Beta','Unknown']);
const commitStart=html.indexOf('function physicalCommitPrinting(mode)');
const commitEnd=html.indexOf('const PHYSICAL_SORT_KEY=',commitStart);
assert.ok(commitStart>0&&commitEnd>commitStart);
assert.ok(html.slice(commitStart,commitEnd).includes('const leftover=original.qty-candidate.qty;'));
assert.ok(html.slice(commitStart,commitEnd).includes('physicalNormalize(rows)'));
assert.ok(html.slice(commitStart,commitEnd).includes("else rows.push(candidate)"));
assert.match(html,/physicalPrintingOptions=.*filter\(p=>!p\.digital&&p\.games\?\.includes\('paper'\)\)/);
console.log('PASS: physical collection set release sorting, unknown date handling, and printing selection/assignment contract');


// v5.20: owned-printing image preference, portable sync snapshots, QR LAN pairing,
// and collection-number set drilldown sorting.
for (const item of ['id="physicalArtworkSave"','function setPreferredArt(','function getPreferredArt(',
 'id="portableSyncPanel"','id="portableSyncQr"','id="portableSyncExport"',
 'id="portableSyncFile"','id="portableSyncScope"','id="portableSyncUndo"',
 'function portableSyncSnapshot()','function portableSyncApply(','function portableSyncShowQr()',
 'arcane-vault-collection-sync','window','id="setCardSort"','value="collector"'])
 assert.ok(html.includes(item),'Missing physical art or collection sync feature: '+item);
assert.match(html,/getPreferredArt\(c\.name\)\|\|c\.image/);
assert.match(html,/getPreferredArt\(c\.name\);if\(chosen\)return chosen/);
assert.match(html,/physicalArtworkSave'\)\.onclick/);
assert.match(html,/const rollback=\{format:'arcane-vault-collection-sync'/);
assert.match(html,/save\(PORTABLE_SYNC_ROLLBACK,rollback\)/);
assert.match(html,/QR transfer/);
assert.match(html,/new QRCode\(host,/);
assert.match(html,/setPage=0;loadSetCards\(\)/);
const catalogSyncSrc=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/ScryfallCatalog.cs'),'utf8');
const localSyncSrc=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/LocalAppServer.cs'),'utf8');
const lanSyncSrc=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/LanCollectionTransfer.cs'),'utf8');
assert.ok(catalogSyncSrc.includes('sort=="collector" ? numberOrder'));
assert.ok(catalogSyncSrc.includes('ORDER BY {order} LIMIT $limit OFFSET $offset'));
assert.ok(catalogSyncSrc.includes('collectorNumber=reader.GetString(6)'));
assert.ok(localSyncSrc.includes('/api/sync/qr/start'));
assert.ok(localSyncSrc.includes('lanSync.Start(snapshot, viaUsb: isUsb)'));
assert.ok(localSyncSrc.includes('/api/sync/usb/start'));
assert.ok(html.includes('id="portableSyncUsb"'));
assert.ok(html.includes('function portableSyncShowTransfer(viaUsb=false)'));
assert.ok(lanSyncSrc.includes('IPAddress.Loopback'));
const usbSyncSrc=fs.readFileSync(path.join(root,'ArcaneVault.Desktop/UsbAdbTransfer.cs'),'utf8');
assert.ok(usbSyncSrc.includes('reverse'));
assert.ok(usbSyncSrc.includes('"-d"'));
assert.ok(usbSyncSrc.includes('tcp:{port}'));
assert.ok(lanSyncSrc.includes('RandomNumberGenerator.GetBytes(24)'));
assert.ok(lanSyncSrc.includes('DateTimeOffset.UtcNow.AddMinutes(5)'));
assert.ok(lanSyncSrc.includes('token = null;'));
assert.ok(lanSyncSrc.includes('ExpireAsync(listener, cancellation.Token)'));
assert.ok(lanSyncSrc.includes('bool endOfHeaders = false'),'QR transfer must fully read HTTP request headers before writing');
assert.ok(lanSyncSrc.includes('if (!endOfHeaders) return;'),'QR transfer must reject partial HTTP request headers');
assert.ok(lanSyncSrc.includes('await stream.FlushAsync(cancel);'),'QR transfer must flush JSON response');
assert.ok(lanSyncSrc.includes('SocketShutdown.Send'),'QR transfer must cleanly finish the response');
assert.ok(html.includes('id="avSyncAllData"'),'Missing single card data sync button');
assert.match(html,/async function physicalRefreshValues\(\)/);
assert.match(html,/attempt<=4/,'Physical pricing retries transient batch errors');
assert.match(html,/const checkpoint=\(\)=>/,'Physical pricing checkpoints progress');
assert.match(html,/Partial refresh:/,'Physical pricing distinguishes partial failures');
assert.match(html,/batch\(es\) failed/,'Partial refresh reports failed batches');
assert.match(html,/if\(rateLimited\)break;/,'Valuation must stop on Scryfall throttle');
assert.match(html,/response\.status===429\|\|response\.status===403/,'Rate-limit and access denial must not be retried');
assert.match(html,/checkpoint\(\);/,'Partial prices must be persisted before a rate-limit stop');
assert.match(localSyncSrc,/ScryfallCollectionMinimumGapMs = 700/,'Scryfall collection requests must be conservatively paced');
assert.match(localSyncSrc,/scryfallCooldownUntil/,'API throttling must activate shared cooldown');
assert.match(localSyncSrc,/ctx\.Response\.Headers\["Retry-After"\]/,'Cooldown must be communicated to the UI');
assert.match(localSyncSrc,/if \(response\.StatusCode == \(HttpStatusCode\)429\)/,'429 must be treated separately from retryable errors');
assert.match(localSyncSrc,/response\.StatusCode is HttpStatusCode\.Forbidden or HttpStatusCode\.BadRequest/,'Scryfall 400 and 403 must stop client retries');


assert.ok(html.includes('id="setHideZero"'),'Missing hide zero-progress sets option');
assert.ok(html.includes("setRows.filter(r=>Number(r.owned)>0)"),'Zero-progress sets must be filtered');
assert.ok(html.includes("save('setHideZeroV1'"),'Zero-progress preference must persist');
assert.ok(html.includes('async function avSyncAllCardData()'),'Missing unified synchronization pipeline');
assert.ok(html.includes('const result=await syncAll(true)'),'Sync all must refresh owned-card legalities');

console.log('PASS: collector-number set sorting, saved printing art, portable sync rollback, and single-use LAN QR transfer');
