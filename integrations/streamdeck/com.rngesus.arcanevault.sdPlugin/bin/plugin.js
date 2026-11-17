'use strict';
// Arcane Vault Companion 0.9.2: local read-only snapshot bridge, Stream Deck SDK v2 websocket.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const cp = require('node:child_process');
const { pathToFileURL } = require('node:url');
const ID = 'com.rngesus.arcanevault';
const refreshRequestPath = path.join(process.env.LOCALAPPDATA || path.join(os.homedir(),'AppData','Local'), 'ArcaneVault', 'streamdeck-refresh.request');
const snapshotPath = process.env.ARCANE_VAULT_STREAMDECK_JSON || path.join(process.env.LOCALAPPDATA || path.join(os.homedir(),'AppData','Local'), 'ArcaneVault', 'streamdeck.json');
const selectionStorePath = path.join(path.dirname(snapshotPath), 'streamdeck-selections.json');
const argv = process.argv.slice(2);
const arg = n => { const i=argv.indexOf(n); return i<0 ? undefined : argv[i+1]; };
const port = arg('-port'), uuid = arg('-pluginUUID'), event = arg('-registerEvent');
if (!port || !uuid || !event) { console.error('This executable is launched by the Stream Deck application.'); process.exit(2); }
let socket, snapshot = null, snapshotError = '', modified='';
let requestedAt=0;
const instances = new Map();
const diagnosticFile = path.join(path.dirname(snapshotPath),'streamdeck-companion-diagnostics.log');
function trace(kind, detail='') {
  try {
    fs.mkdirSync(path.dirname(diagnosticFile),{recursive:true});
    fs.appendFileSync(diagnosticFile, new Date().toISOString()+' v0.9.2 '+kind+' '+String(detail).slice(0,160)+'\n');
  } catch(e) { console.error('Diagnostic write failed:',e.message); }
}
trace('plugin-start',ID);

// Stream Deck's setSettings is the primary persistence API. Mirror the user's
// per-key selection locally to recover from missing, stale or late host settings.
// This is UI preference only: no deck, inventory or match data is changed.
const pendingSelections = new Map();
const selections = (()=>{
  try {
    const stat=fs.statSync(selectionStorePath);
    if(stat.size>1024*1024) return {};
    const obj=JSON.parse(fs.readFileSync(selectionStorePath,'utf8'));
    return obj && typeof obj==='object' && !Array.isArray(obj) ? obj : {};
  }catch{return {};}
})();
function normalizedDeckSettings(data) {
  const id=typeof data?.deckId==='string'?data.deckId:'';
  const name=typeof data?.deckName==='string'?data.deckName:'';
  return {deckId:id.slice(0,256),deckName:name.slice(0,256)};
}
function persistSelection(context, value) {
  if(!context || context.length>256) return;
  const selected=normalizedDeckSettings(value);
  if(!selected.deckId && !selected.deckName) delete selections[context];
  else selections[context]=selected;
  try {
    fs.mkdirSync(path.dirname(selectionStorePath),{recursive:true});
    const temp=selectionStorePath+'.tmp';
    fs.writeFileSync(temp,JSON.stringify(selections),'utf8');
    fs.renameSync(temp,selectionStorePath);
  }catch(e){console.error('Could not persist Stream Deck selection',e.message);}
}
function restoredSettings(context, incoming) {
  const fromHost=incoming && typeof incoming==='object' ? incoming : {};
  const local=selections[context];
  if(!local || (!local.deckId && !local.deckName)) return {...fromHost};
  // The explicit choice in the PI is authoritative over a stale/missing host
  // didReceiveSettings event, including after closing and reopening the PI.
  return {...fromHost,...local};
}

const sanitize = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));
const shorten = (s,n=16) => {s=String(s||''); return s.length>n ? s.slice(0,n-1)+'…' : s;};
const send = obj => {if(socket && socket.readyState===1) socket.send(JSON.stringify(obj));};
function load() {
  try {
    const stat=fs.statSync(snapshotPath);
    if(stat.size>8*1024*1024) throw Error('snapshot exceeds 8MB');
    const stamp = stat.mtimeMs + ':' + stat.size;
    if(stamp!==modified || !snapshot) {
      const next=JSON.parse(fs.readFileSync(snapshotPath,'utf8'));
      if(!next || !Array.isArray(next.decks)) throw Error('snapshot requires decks array');
      snapshot=next; modified=stamp;
    }
    snapshotError='';
  } catch(e) {
    // A snapshot can briefly disappear during a native update or migration.
    // Keep the last verified data rather than replacing a tracked deck with NO DATA.
    snapshotError=e.code==='ENOENT'?'Awaiting Arcane Vault snapshot':String(e.message);
    trace('snapshot-read-retry', snapshotError);
  }
}
function artImage(deck) {
  const file=deck && typeof deck.imagePath==='string' ? deck.imagePath : '';
  if(!file || !path.isAbsolute(file)) return '';
  try {
    const stat=fs.statSync(file);
    if(stat.size>2*1024*1024) return '';
    const ext=path.extname(file).toLowerCase();
    const mime=ext==='.png'?'image/png':(['.jpg','.jpeg'].includes(ext)?'image/jpeg':'');
    if(!mime) return '';
    return 'data:'+mime+';base64,'+fs.readFileSync(file).toString('base64');
  }catch {return '';}
}
function svgFor(type, data, deck) {
  const w=144, h=144;
  const bg=artImage(deck);
  const title=deck ? shorten(deck.name,21) : (type==='deck'?'SELECT DECK':'ARCANE VAULT');
  const wins=Number(deck?.wins), losses=Number(deck?.losses);
  const hasRecord=Number.isFinite(wins)&&Number.isFinite(losses)&&wins>=0&&losses>=0;
  const record=deck && hasRecord ? wins+'-'+losses : '—';
  const pct=deck && hasRecord&&wins+losses>0 ? Math.round(wins/(wins+losses)*100)+'% WIN' : (deck ? 'NO MATCHES' : 'CHOOSE A DECK');
  const box=`<rect width="144" height="144" fill="#17212c"/>`;
  const image=bg ? `<image href="${bg}" x="0" y="0" width="144" height="144" preserveAspectRatio="xMidYMid slice"/>` : `<path d="M0 110 L80 0 L144 30 L144 144 L0 144Z" fill="#29394b"/><path d="M0 28 L95 0 L144 90 L25 144Z" fill="#534035" opacity=".5"/>`;
  const overlay=`<rect x="0" y="0" width="144" height="144" fill="url(#shade)"/>`;
  const txt=(x,y,s,size=16,col='#fff',weight=700)=>`<text x="${x}" y="${y}" font-family="Arial, sans-serif" font-size="${size}" font-weight="${weight}" text-anchor="middle" fill="${col}">${sanitize(s)}</text>`;
  let content='';
  switch(type){
    case 'deck': case 'current':
      content=txt(72,24,title,13,'#fff',700)+txt(72,92,record,35,'#fff')+txt(72,118,pct,15,'#92e0ad');break;
    case 'value': {
      const raw=snapshot?.collection?.value;
      const n=(raw===null || raw===undefined || raw==='')?NaN:Number(raw);
      const currency=typeof snapshot?.collection?.currency==='string'?snapshot.collection.currency:'USD';
      const amount=Number.isFinite(n)? new Intl.NumberFormat('en-US',{style:'currency',currency:currency==='USD'?'USD':currency,maximumFractionDigits:0}).format(n):'—';
      content=txt(72,33,'COLLECTION',13)+txt(72,90,shorten(amount,12),24,'#e7c58c')+txt(72,114,'PHYSICAL VALUE',11,'#eee');break;
    }
    case 'sync': {
      const info=snapshot?.sync || {};
      content=txt(72,34,'DATA & SYNC',15)+txt(72,85,shorten(info.status||'UNKNOWN',13),21,info.status==='Up to date'?'#9ee5ab':'#f1ca89')+txt(72,109,shorten(info.lastSync||'No sync recorded',20),11);break;
    }
    case 'recent': {
      const r=snapshot?.recentMatch || {};
      const status=String(r.result||'').toLowerCase();
      content=txt(72,33,'LATEST MATCH',14)+txt(72,83,status==='win'?'WIN':status==='loss'?'LOSS':'NO MATCH',24,status==='win'?'#95e4ad':status==='loss'?'#f2a0a0':'#fff')+txt(72,110,shorten(r.deckName||'',19),12);break;
    }
    case 'refresh': content=txt(72,36,'REFRESH',19,'#f2ce91')+txt(72,74,'DECK STATS',18)+txt(72,110,'PRESS TO SYNC',12);break;
    case 'open': content=txt(72,42,'ARCANE',22,'#f2ce91')+txt(72,68,'VAULT',24)+txt(72,110,'OPEN APP',13);break;
  }
  if(!snapshot && type!=='open')content=txt(72,24,'ARCANE VAULT',13)+txt(72,80,'NO DATA',20,'#edc289')+txt(72,103,'Check snapshot',12);
  return `<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144"><defs><linearGradient id="shade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#000" stop-opacity=".55"/><stop offset=".42" stop-color="#000" stop-opacity=".25"/><stop offset="1" stop-color="#000" stop-opacity=".85"/></linearGradient></defs>${box}${image}${overlay}${content}</svg>`;
}
function selectDeck(instance) {
  const decks=snapshot?.decks||[];
  if(instance.action.endsWith('.current')) return decks.find(d=>String(d.id)===String(snapshot?.activeDeckId));
  const id=String(instance.settings?.deckId??'');
  const name=String(instance.settings?.deckName??'');
  if(id) return decks.find(d=>String(d.id)===id);
  // Legacy name-only selection is permitted when no stable ID exists.
  return name ? decks.find(d=>String(d.name).toLowerCase()===name.toLowerCase()) : undefined;
}
function refresh(instance) {
  const type=instance.action.slice(ID.length+1),deck=selectDeck(instance);
  // The native app supplies a fully composited PNG (commander art + record).
  // Direct PNG images are more reliable on hardware than an SVG containing
  // another data-URI image. Keep the SVG fallback for older native builds.
  if ((type==='deck' || type==='current') && deck?.keyImagePath) {
    try {
      const file=deck.keyImagePath;
      if(path.isAbsolute(file) && path.extname(file).toLowerCase()==='.png' &&
         fs.statSync(file).size<2*1024*1024) {
        const img='data:image/png;base64,'+fs.readFileSync(file).toString('base64');
        send({event:'setImage',context:instance.context,payload:{image:img,target:0}});
        return;
      }
    }catch{}
  }
  const svg=svgFor(type,snapshot,deck);
  send({event:'setImage',context:instance.context,payload:{image:'data:image/svg+xml;base64,'+Buffer.from(svg,'utf8').toString('base64'),target:0}});
}
function requestFreshStats(deckId='') {
  try {
    fs.mkdirSync(path.dirname(refreshRequestPath), {recursive:true});
    const id=typeof deckId==='string'&&deckId.length<=160?deckId:'';
    fs.writeFileSync(refreshRequestPath,JSON.stringify({nonce:Date.now()+'-'+(++requestedAt),deckId:id}), 'utf8');
    return true;
  }catch(e){console.error('Stream Deck refresh request failed',e.message);return false;}
}

function broadcast(){load(); for(const i of instances.values()) refresh(i);}
function updateDeckSelection(context, deckId, deckName) {
  let instance=instances.get(context);
  if(!instance) {
    instance={context,action:ID+'.deck',settings:{}};
    instances.set(context,instance);
  }
  if(instance.action!==ID+'.deck') return;
  const choice=normalizedDeckSettings({deckId,deckName});
  instance.settings={...instance.settings,...choice};
  persistSelection(context, choice);
  pendingSelections.set(context, {deckId:choice.deckId, until:Date.now()+10000});
  trace('deckSelected-received', 'deckId='+choice.deckId+' contextPresent='+Boolean(context));
  // Keep Stream Deck's built-in settings as the primary store too.
  send({event:'setSettings',action:instance.action,context,payload:instance.settings});
  load();refresh(instance);
  requestFreshStats(choice.deckId);
  send({event:'sendToPropertyInspector',action:instance.action,context,payload:{type:'selectionAck',deckId:choice.deckId,deckName:choice.deckName,settings:instance.settings,via:'sendToPlugin'}});
  trace('selectionAck-sent','deckId='+choice.deckId);
  inspector(context);
}
// Stream Deck key-press fallback: works entirely within the plugin-side SDK
// websocket even when the host never forwards PI sendToPlugin messages.
// Selection is stored under this key's context, and refreshed with its image.
function cycleDeck(context, action) {
  load();
  const decks=(snapshot?.decks||[]).filter(d=>String(d.id||'').length>0);
  if(decks.length===0){ trace('cycle-no-decks');return false; }
  let instance=instances.get(context);
  if(!instance) {
    instance={context,action,settings:restoredSettings(context,{})};
    instances.set(context,instance);
  }
  instance.settings=restoredSettings(context,instance.settings);
  const current=decks.findIndex(d=>String(d.id)===String(instance.settings.deckId||''));
  const selected=decks[(current+1)%decks.length];
  const choice=normalizedDeckSettings({deckId:String(selected.id),deckName:String(selected.name)});
  instance.settings={...instance.settings,...choice};
  persistSelection(context,choice);
  pendingSelections.set(context, {deckId:choice.deckId, until:Date.now()+10000});
  send({event:'setSettings',action,context,payload:instance.settings});
  trace('key-cycle-selected','deck='+choice.deckName+' id='+choice.deckId);
  refresh(instance);
  requestFreshStats(choice.deckId);
  // The PI may be open, or may be unable to communicate reliably. This is
  // optional presentation feedback, not needed for persistence.
  inspector(context);
  return true;
}
function instanceSettings(context) {return restoredSettings(context,instances.get(context)?.settings||{});}
function inspector(context) {
  if(!instances.has(context)) instances.set(context,{context,action:ID+'.deck',settings:restoredSettings(context,{})});
  const selected=selectDeck(instances.get(context)||{action:ID+'.deck',settings:{}});
  const visual=selected ? (selected.artStatus || (selected.keyImagePath ? 'Tile ready' : selected.artUrl ? 'Artwork loading' : 'No official artwork URL found')) : 'No deck selected';
  trace('decks-response','contextPresent='+Boolean(context));
  send({event:'sendToPropertyInspector',action:ID+'.deck',context,payload:{type:'decks',decks:(snapshot?.decks||[]).map(d=>({id:String(d.id),name:String(d.name)})),error:snapshotError,selected:visual,settings:instanceSettings(context),selectedDeckId:selected?.id||''}});
}
function openApp(){
  const exe=snapshot?.appPath;
  if(typeof exe==='string'&&path.isAbsolute(exe)&&exe.toLowerCase().endsWith('.exe')&&fs.existsSync(exe)) {
    try{cp.spawn(exe,[],{detached:true,stdio:'ignore'}).unref(); return true;}catch{}
  }
  return false;
}
function receive(msg){
  let m; try{m=JSON.parse(msg);}catch{return;}
  const c=m.context;
  switch(m.event){
    case 'willAppear': instances.set(c,{context:c,action:m.action,settings:restoredSettings(c,m.payload?.settings)});load();refresh(instances.get(c));break;
    case 'willDisappear':instances.delete(c);pendingSelections.delete(c);break;
    case 'didReceiveSettings': {
      const i=instances.get(c);
      const host=m.payload?.settings||{};
      trace('didReceiveSettings','deckId='+String(host.deckId||'')+' contextPresent='+Boolean(c));
      if(i){
        if(i.action===ID+'.deck' && typeof host.deckId==='string' && host.deckId) {
          const pending=pendingSelections.get(c);
          const isOldResponse=pending && Date.now()<pending.until &&
            String(host.deckId)!==pending.deckId;
          if(isOldResponse) {
            trace('ignored-stale-host-settings','deckId='+host.deckId);
          } else {
            // A Property Inspector change must overwrite the previous local
            // preference. Earlier builds only saved if no cache entry existed,
            // permanently restoring an old deck on reload.
            persistSelection(c,host);
            pendingSelections.delete(c);
            trace('host-settings-saved','deckId='+host.deckId);
          }
        }
        i.settings=restoredSettings(c,host);load();refresh(i);
        if(i.action===ID+'.deck') {
          requestFreshStats(i.settings?.deckId||'');
          send({event:'sendToPropertyInspector',action:i.action,context:c,
                payload:{type:'selectionAck',deckId:i.settings?.deckId||'',
                         deckName:i.settings?.deckName||'',via:'persisted-settings'}});
        }
      }
      break;
    }
    case 'propertyInspectorDidAppear':trace('PI-open',Boolean(c));load();send({event:'getSettings',action:m.action||ID+'.deck',context:c});inspector(c);break;
    case 'sendToPlugin':
      trace('sendToPlugin',m.payload?.type||'none');
      if(m.payload?.type==='getDecks'){load();inspector(c);}
      else if(m.payload?.type==='deckSelected'){
        updateDeckSelection(c,m.payload.deckId,m.payload.deckName);
      }
      else if(m.payload?.type==='refreshStats'){
        requestFreshStats(m.payload.deckId||'');broadcast();inspector(c);
      }
      break;
    case 'keyDown': {
      if(m.action===ID+'.open') {if(openApp())send({event:'showOk',context:c});else send({event:'showAlert',context:c});}
      else if(m.action===ID+'.deck') {
        if(cycleDeck(c,m.action)) send({event:'showOk',context:c});
        else send({event:'showAlert',context:c});
      }
      else if(m.action===ID+'.refresh' || m.action===ID+'.current') {
        const i=instances.get(c);
        if(requestFreshStats(i?.settings?.deckId||'')){broadcast();send({event:'showOk',context:c});}
        else send({event:'showAlert',context:c});
      } else {broadcast();send({event:'showOk',context:c});}
      break;
    }
  }
}
const ws=new WebSocket('ws://127.0.0.1:'+port);socket=ws;
ws.addEventListener('open',()=>{send({event,uuid});broadcast();});
ws.addEventListener('message',e=>receive(String(e.data)));
ws.addEventListener('error',e=>console.error('Stream Deck connection error',e.message||e));
ws.addEventListener('close',()=>process.exit(0));
setInterval(broadcast,5000).unref?.();
