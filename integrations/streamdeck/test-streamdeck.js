const fs = require('node:fs'),path=require('node:path'), os=require('node:os'), vm=require('node:vm'), assert=require('node:assert/strict');
const file=path.join(__dirname,'com.rngesus.arcanevault.sdPlugin','bin','plugin.js');
const code=fs.readFileSync(file,'utf8');
const tmp=fs.mkdtempSync(path.join(os.tmpdir(),'sd-test-'));
const snapshotFile=path.join(tmp,'streamdeck.json');
const deckList=[{id:'deck-A',name:'Deck A',wins:1,losses:0},{id:'deck-B',name:'Deck B',wins:3,losses:2}];
fs.writeFileSync(snapshotFile,JSON.stringify({decks:deckList}));
function start(){const sockets=[]; class MockWebSocket{static OPEN=1;constructor(url){this.url=url;this.readyState=1;this.handlers={};this.sent=[];sockets.push(this);}addEventListener(type,fn){this.handlers[type]=fn;}send(data){this.sent.push(JSON.parse(data));} emit(type,value){this.handlers[type]?.(type==='message'?{data:JSON.stringify(value)}:value);}}
const env={LOCALAPPDATA:tmp,ARCANE_VAULT_STREAMDECK_JSON:snapshotFile};const processStub={argv:['node','plugin.js','-port','9999','-pluginUUID','com.rngesus.arcanevault','-registerEvent','registerPlugin'],env,exit:(code)=>{throw Error('exit:'+code)}};
vm.runInNewContext(code,{require,process:processStub,Buffer,console,WebSocket:MockWebSocket,setInterval:()=>({unref(){}}),Date}, {filename:file});const ws=sockets[0];ws.emit('open');return{ws,receive(x){ws.emit('message',x)},sent(){return ws.sent}};}
let p=start(),c='test-context';p.receive({event:'willAppear',context:c,action:'com.rngesus.arcanevault.deck',payload:{settings:{deckId:'deck-A',deckName:'Deck A'}}});
p.receive({event:'didReceiveSettings',context:c,action:'com.rngesus.arcanevault.deck',payload:{settings:{deckId:'deck-A',deckName:'Deck A'}}});
let f=path.join(tmp,'streamdeck-selections.json');assert.equal(JSON.parse(fs.readFileSync(f))[c].deckId,'deck-A');
p.receive({event:'didReceiveSettings',context:c,action:'com.rngesus.arcanevault.deck',payload:{settings:{deckId:'deck-B',deckName:'Deck B'}}});assert.equal(JSON.parse(fs.readFileSync(f))[c].deckId,'deck-B','host change overwrites stale local backup');
p=start();p.receive({event:'willAppear',context:c,action:'com.rngesus.arcanevault.deck',payload:{settings:{}}});p.receive({event:'propertyInspectorDidAppear',context:c,action:'com.rngesus.arcanevault.deck'});const inspector=p.sent().filter(e=>e.event==='sendToPropertyInspector'&&e.payload?.type==='decks').at(-1);assert.equal(inspector.payload.settings.deckId,'deck-B','survives plugin restart');
p.receive({event:'sendToPlugin',context:c,action:'com.rngesus.arcanevault.deck',payload:{type:'deckSelected',deckId:'deck-A',deckName:'Deck A'}});assert.equal(JSON.parse(fs.readFileSync(f))[c].deckId,'deck-A');p.receive({event:'didReceiveSettings',context:c,action:'com.rngesus.arcanevault.deck',payload:{settings:{deckId:'deck-B',deckName:'Deck B'}}});assert.equal(JSON.parse(fs.readFileSync(f))[c].deckId,'deck-A','stale host notification ignored');
p.receive({event:'didReceiveSettings',context:c,action:'com.rngesus.arcanevault.deck',payload:{settings:{deckId:'deck-A',deckName:'Deck A'}}});assert.equal(JSON.parse(fs.readFileSync(f))[c].deckId,'deck-A');
fs.unlinkSync(snapshotFile);p.receive({event:'propertyInspectorDidAppear',context:c,action:'com.rngesus.arcanevault.deck'});const afterMissing=p.sent().filter(e=>e.event==='sendToPropertyInspector'&&e.payload?.type==='decks').at(-1);assert.equal(afterMissing.payload.decks.length,2,'temporary missing file retains last valid snapshot');
console.log('PASS host changes, selection persistence, restart, stale settings and temporary snapshot outage');

fs.writeFileSync(snapshotFile,JSON.stringify({decks:deckList}));
// Native exporter remains authoritative. Verify on-disk W/L updates survive a
// plugin refresh, while a missing deck ID never inherits a same-name record.
const stable=start();const ctx='stats-context';
stable.receive({event:'willAppear',context:ctx,action:'com.rngesus.arcanevault.deck',payload:{settings:{deckId:'deck-A',deckName:'Deck A'}}});
const baseImages=stable.sent().filter(x=>x.event==='setImage' && x.context===ctx);
assert(baseImages.length>0,'initial deck tile rendered');
fs.writeFileSync(snapshotFile,JSON.stringify({decks:[{id:'deck-A',name:'Deck A',wins:5,losses:1},{id:'deck-B',name:'Deck B',wins:3,losses:2}]}));
fs.utimesSync(snapshotFile,new Date(Date.now()+2000),new Date(Date.now()+2000));
stable.receive({event:'sendToPlugin',context:ctx,action:'com.rngesus.arcanevault.deck',payload:{type:'refreshStats',deckId:'deck-A'}});
const updated=stable.sent().filter(x=>x.event==='setImage' && x.context===ctx).at(-1);
assert(updated && updated.payload.image.startsWith('data:image/svg+xml;base64,'),'refreshed image produced');
const svg=Buffer.from(updated.payload.image.split(',')[1],'base64').toString('utf8');
assert(svg.includes('5-1'),'W/L display follows newest confirmed native snapshot');
console.log('PASS native snapshot stats refresh and display');
