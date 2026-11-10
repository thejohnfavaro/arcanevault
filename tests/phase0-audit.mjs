// Phase 0 tests are synthetic and never open an installed user's profile.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { createServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import { inspectBackup, compareBackups } from '../tools/phase0-audit.mjs';
import { summarizeSamples, measure } from '../tools/phase0-benchmark.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const script=path.join(root,'tools','phase0-audit.mjs');
const folder=fs.mkdtempSync(path.join(os.tmpdir(),'arcane-audit-test-'));
const write=(name,data)=>{const f=path.join(folder,name);fs.writeFileSync(f,JSON.stringify(data));return f;};
const printingId='00000000-0000-4000-8000-000000000001';
const pristine={
  schema:2,
  cards:[{name:'Example Spell',qty:2,sets:['TST']},{name:'Example Dual // Example Back',qty:0,sets:['TST']}],
  physicalCards:[
    {name:'Example Spell',qty:1,set:'tst',number:'1',finish:'nonfoil',scryfallId:printingId},
    {name:'Example Spell',qty:3,set:'a01',number:'2',finish:'foil',location:'Binder'},
    {name:'Unresolved Artifact',qty:2,set:'',number:'',finish:'nonfoil'}
  ],
  deckLibrary:[{id:'d1',name:'Example deck',commander:'Example Spell',entries:[{name:'Example Spell',qty:2}],revisions:[{name:'Earlier',entries:[]}]}],
  preferredArtwork:{'example spell':{image:'https://cards.scryfall.io/test.png',id:printingId}}
};
try {
  const first=write('before.json',pristine);
  const priorBytes=fs.readFileSync(first);
  const priorStat=fs.statSync(first);
  const audit=inspectBackup(first);
  assert.equal(audit.format,'full-backup');
  assert.equal(audit.counts.arenaCopies,2);
  assert.equal(audit.counts.physicalRows,3);
  assert.equal(audit.counts.physicalCopies,6);
  assert.equal(audit.counts.identifiedCopies,4);
  assert.equal(audit.counts.unidentifiedCopies,2);
  assert.equal(audit.counts.deckRevisions,1);
  assert.deepEqual(audit.warnings,['UNRESOLVED_PHYSICAL_PRINTINGS']);
  assert.deepEqual(fs.readFileSync(first),priorBytes,'Audit must not change backup bytes');
  assert.equal(fs.statSync(first).mtimeMs,priorStat.mtimeMs,'Audit must not touch input metadata');

  const reordered=structuredClone(pristine);
  reordered.physicalCards.reverse();
  const reorderFile=write('reorder.json',reordered);
  assert.deepEqual(compareBackups(first,reorderFile),[],'Row ordering alone must not change identity');
  const dup=structuredClone(pristine);
  dup.physicalCards.push({...dup.physicalCards[0],qty:2});
  assert.ok(inspectBackup(write('duplicate.json',dup)).warnings.includes('DUPLICATE_PHYSICAL_IDENTITIES'));

  const changed=structuredClone(pristine);
  changed.physicalCards[0].qty=5;
  const changedFile=write('changed.json',changed);
  const differences=compareBackups(first,changedFile);
  assert.ok(differences.includes('physicalCopies'));
  assert.ok(differences.includes('physicalContent'));
  const mismatch=spawnSync(process.execPath,[script,'--backup',first,'--compare',changedFile],{encoding:'utf8'});
  assert.equal(mismatch.status,3);
  assert.ok(mismatch.stdout.includes('"exactContentMatch": false'));
  assert.ok(!mismatch.stdout.includes('Example Spell'),'Report must not expose card names');

  const unchanged=spawnSync(process.execPath,[script,'--backup',first,'--compare',reorderFile],{encoding:'utf8'});
  assert.equal(unchanged.status,0,unchanged.stderr);
  const invalid=spawnSync(process.execPath,[script,'--backup',write('invalid.json',{schema:2,cards:[{name:'Invalid',qty:-1}]})],{encoding:'utf8'});
  assert.equal(invalid.status,2,'Invalid data must fail closed');
  assert.ok(!invalid.stderr.includes('Invalid"}'),'Error should not leak JSON');
  const syncFile=write('sync.json',{
    format:'arcane-vault-collection-sync',schema:1,
    physicalCards:pristine.physicalCards,arenaCards:pristine.cards,
    preferredArtwork:pristine.preferredArtwork
  });
  assert.equal(inspectBackup(syncFile).format,'collection-sync-v1');
  assert.ok(compareBackups(first,syncFile).includes('decksContent'),'Missing decks must not be treated as preserved');

  assert.deepEqual(summarizeSamples([1,2,3,4,5]),{count:5,minMs:1,p50Ms:3,p95Ms:5,maxMs:5});
  const server=createServer((req,res)=>{
    res.setHeader('content-type','application/json');
    if(req.url==='/api/catalog/status')res.end(JSON.stringify({total:300,busy:false}));
    else if(req.url.startsWith('/api/catalog/'))res.end(JSON.stringify({sets:[],cards:[],total:0}));
    else{res.statusCode=404;res.end('{}');}
  });
  await new Promise((ok,fail)=>server.once('error',fail).listen(0,'127.0.0.1',ok));
  try{
    const port=server.address().port;
    const report=await measure({url:'http://127.0.0.1:'+port+'/',iterations:3});
    assert.equal(report.readonly,true);
    assert.equal(report.tasks.length,4);
    assert.ok(report.tasks.every(t=>t.status==='ok'),'Local read-only endpoints should all be sampled');
    assert.equal(report.tasks[0].count,3);
    assert.ok(report.tasks.every(t=>t.p95Ms>=0));
  }finally{await new Promise(ok=>server.close(ok));}
  console.log('PASS: read-only audit, exact printing preservation, mismatch detection and localhost latency sampling');
}finally{fs.rmSync(folder,{recursive:true,force:true});}
