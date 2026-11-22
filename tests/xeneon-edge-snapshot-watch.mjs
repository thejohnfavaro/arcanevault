import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
const source=fs.readFileSync("integrations/xeneon-edge/scripts/widget.js","utf8");
const markup=fs.readFileSync("integrations/xeneon-edge/index.html","utf8");
const stylesheet=fs.readFileSync("integrations/xeneon-edge/styles/widget.css","utf8");
assert.match(markup,/<details class="rank-meta">[\s\S]*?<summary id="rankAge">/,
  "Rank timestamp/prediction detail must remain available in expandable row");
assert.match(markup,/<div class="collection-inline">/,
  "Collection total should remain visible in compact single-line layout");
assert.match(markup, /id="setRows"[^>]+tabindex="0"/,
  "Set list remains reachable by keyboard");
assert.match(stylesheet,/\.side #setPanel\{[\s\S]*?flex:1 1 0;min-height:130px/,
  "Set Progress must receive flexible remaining column height");
assert.match(stylesheet,/\.side #setPanel \.set-rows\{[\s\S]*?overflow-y:auto/,
  "Set list must be vertically scrollable when many rows are present");

// Verify zoom is disabled without sacrificing one-finger Set Progress scrolling.
assert.match(markup, /maximum-scale=1\.0, user-scalable=no/,
  "The embedded viewport must disable touch scaling.");
assert.match(stylesheet, /html,body\{touch-action:pan-x pan-y/,
  "The dashboard must omit pinch zoom from permitted touch actions.");
assert.match(stylesheet, /\.set-rows\{touch-action:pan-y\}/,
  "Set Progress must retain one-finger vertical scrolling.");

const eventListeners = new Map();
const elements=new Map();
const make=(id="")=>{
  const attrs=new Map(), classes=new Set();
  const node={
    id, textContent:"", value:"", disabled:false, hidden:false, style:{},
    dataset:{}, options:[], children:[],
    classList:{add:c=>classes.add(c),remove:c=>classes.delete(c),
      contains:c=>classes.has(c),toggle:(c,on)=>on?classes.add(c):classes.delete(c)},
    listeners:new Map(),
    addEventListener(type,callback){this.listeners.set(type,callback);},
    dispatch(type,value){const callback=this.listeners.get(type);
      assert.equal(typeof callback,"function","Missing "+type+" handler for "+this.id);
      callback({target:{checked:value}});},
    setAttribute:(k,v)=>attrs.set(k,v),getAttribute:k=>attrs.get(k),
    removeAttribute:k=>attrs.delete(k),
    replaceChildren(...items){this.children=items;this.options=items;},
    appendChild(item){this.children.push(item);}
  };
  return node;
};
const document={
  getElementById(id){if(!elements.has(id))elements.set(id,make(id));return elements.get(id);},
  createElement:()=>make(),
  addEventListener(type,callback,options){
    assert.equal(options?.passive,false,"Zoom guards must be non-passive.");
    eventListeners.set(type,callback);
  }
};
let listener, tick, watches=0, unwatches=0, now=100000;
const provider={
  fileChanged:{connect(cb){assert.equal(listener,undefined);listener=cb;}},
  watchPath(p){assert.match(p,/streamdeck\.json$/);watches++;},
  unwatchPath(){unwatches++;}
};
const storage=new Map();
const box={
  document,window:{plugins:{Filesystemprovider:provider}},
  localStorage:{getItem:key=>storage.get(key)??null,
    setItem(key,value){storage.set(key,String(value));},
    removeItem(key){storage.delete(key);}},
  Option:class {constructor(label,value){this.textContent=label;this.value=value;}},
  Date:class extends Date {static now(){return now;}},
  setInterval(fn,ms){assert.equal(ms,5000);tick=fn;},
  pluginFilesystemprovider_initialized:true,
  URL,Intl,console
};
vm.runInNewContext(source,box,{filename:"widget.js"});
for(const type of ["gesturestart","gesturechange","gestureend","touchmove","wheel"])
  assert.equal(typeof eventListeners.get(type),"function",type+" zoom guard must be registered");
function dispatch(type,properties={}) {
  let prevented=false;
  eventListeners.get(type)({...properties,preventDefault(){prevented=true;}});
  return prevented;
}
assert.equal(dispatch("gesturestart"),true,"Pinch start must not zoom.");
assert.equal(dispatch("gesturechange"),true,"Pinch updates must not zoom.");
assert.equal(dispatch("touchmove",{touches:[{},{}]}),true,"Two fingers cannot zoom.");
assert.equal(dispatch("touchmove",{touches:[{}]}),false,
  "Single-finger scrolling must continue working.");
assert.equal(dispatch("wheel",{ctrlKey:true}),true,"Ctrl-wheel must not zoom.");
assert.equal(dispatch("wheel",{ctrlKey:false}),false,"Normal wheel scrolling must work.");

assert.equal(watches,1,"Widget must register snapshot watch on startup");
assert.equal(typeof listener,"function");
assert.match(document.getElementById("status").textContent,/Waiting for Arcane Vault heartbeat/);
listener("C:\\Users\\Test\\AppData\\Local\\ArcaneVault\\streamdeck.json",
  JSON.stringify({decks:[],schemaVersion:1,rank:{constructedClass:"Gold",
    constructedLevel:2,limitedClass:"Platinum",limitedLevel:3,
    asOfUtc:"2026-11-18T13:00:00Z"},
    season:{season:"November 2026",endsUtc:new Date(now+3*86400000).toISOString(),
      checkedUtc:new Date(now).toISOString()},
    predictedConstructed:"Silver Tier 3",predictedLimited:"Gold Tier 4"}));
assert.match(document.getElementById("status").textContent,/no Arena Brawl decks/);
assert.equal(document.getElementById("constructedRank").textContent,"Gold 2");
assert.equal(document.getElementById("limitedRank").textContent,"Platinum 3");
assert.match(document.getElementById("constructedRankIcon").getAttribute("src"),/constructed_gold_t2\.png$/);
assert.match(document.getElementById("seasonCountdown").textContent,/3d 0h 0m/);
assert.match(document.getElementById("seasonPrediction").textContent,/Silver Tier 3/);
// Only Arena Brawl appears on the EDGE, never paper Commander or other formats.
listener("C:\\\\Users\\\\Test\\\\AppData\\\\Local\\\\ArcaneVault\\\\streamdeck.json",
  JSON.stringify({schemaVersion:1,decks:[
    {id:"paper-1",name:"Paper Commander",format:"Commander",commander:"Atraxa"},
    {id:"brawl-1",name:"Arena Brawl",format:"Arena Brawl",commander:"The Borg Queen",wins:2,losses:1},
    {id:"modern-1",name:"Modern",format:"Modern",commander:""}
  ]}));
assert.equal(document.getElementById("deckName").textContent,"Arena Brawl",
  "Only Arena Brawl should be available in the EDGE selector");
assert.equal(document.getElementById("deckCount").textContent,"1 / 1");
assert.equal(document.getElementById("wins").textContent,"2");
assert.match(markup,/id="autoSwitchDeck"/,
  "Widget settings must expose auto-switch deck toggle");
const settings=document.getElementById("autoSwitchDeck");
assert.equal(settings.checked,true,"Automatic switching should start enabled");
const path="C:\\\\Users\\\\Test\\\\AppData\\\\Local\\\\ArcaneVault\\\\streamdeck.json";
const first={id:"brawl-1",name:"Borg",format:"Arena Brawl",wins:2,losses:1,
  matchHistory:[{result:"win",playedUtc:"2026-11-22T08:00:00Z"}]};
const second={id:"brawl-2",name:"Etali",format:"Arena Brawl",wins:0,losses:0,
  matchHistory:[]};
const emit=(decks,extras={})=>listener(path,JSON.stringify({schemaVersion:1,
  publishedUtc:"2026-11-22T14:00:00Z",decks,...extras}));
// First seen snapshot establishes baseline, never switches to a historical winner.
emit([first,second]);
assert.equal(document.getElementById("deckName").textContent,"Borg");
document.getElementById("next").dispatch("click");
assert.equal(document.getElementById("deckName").textContent,"Etali");
assert.equal(storage.get("arcanevault.edge.deckId.v1"),"brawl-2");
// Unrelated heartbeats and changes in artwork should not steal the selection.
emit([{...first,artUrl:"https://cards.scryfall.io/normal/front/a/a/new.jpg"},second]);
assert.equal(document.getElementById("deckName").textContent,"Etali");
emit([{...first,wins:3,matchHistory:[{result:"win",playedUtc:"2026-11-22T10:00:00Z"}]},
  second]);
assert.equal(document.getElementById("deckName").textContent,"Borg",
  "A new confirmed result should switch to the updated deck");
assert.equal(storage.get("arcanevault.edge.deckId.v1"),"brawl-2",
  "Automatic display changes must not overwrite manual deck preference");
// A continued heartbeat with the same results must be stable.
emit([{...first,wins:3},second]);
assert.equal(document.getElementById("deckName").textContent,"Borg");
// Turning off must persist; manual arrows continue to work.
settings.dispatch("change",false);
assert.equal(storage.get("arcanevault.edge.autoSwitchOnMatch.v1"),"false");
emit([{...first,wins:3},{...second,losses:1,
  matchHistory:[{result:"loss",playedUtc:"2026-11-22T11:00:00Z"}]}]);
assert.equal(document.getElementById("deckName").textContent,"Borg");
document.getElementById("next").dispatch("click");
assert.equal(document.getElementById("deckName").textContent,"Etali");
settings.dispatch("change",true);
assert.equal(storage.get("arcanevault.edge.autoSwitchOnMatch.v1"),"true");
emit([{...first,wins:3},{...second,losses:1}]);
assert.equal(document.getElementById("deckName").textContent,"Etali",
  "Enabling the option must not replay past changes");
emit([{...first,wins:3,losses:2},{...second,losses:1}]);
assert.equal(document.getElementById("deckName").textContent,"Borg",
  "A new loss on another deck should switch to it");
// A newly added saved deck with old matches is not a new match.
emit([{...first,wins:3,losses:2},{...second,losses:1},
  {id:"brawl-3",name:"Newly synced",format:"Arena Brawl",wins:9,losses:1}]);
assert.equal(document.getElementById("deckName").textContent,"Borg");
listener("C:\\\\Users\\\\Test\\\\AppData\\\\Local\\\\ArcaneVault\\\\streamdeck.json",
  JSON.stringify({schemaVersion:1,decks:[
    {id:"paper-only",name:"Paper Commander",format:"Commander",commander:"Atraxa"}
  ]}));
assert.equal(document.getElementById("deckName").textContent,"No Arena Brawl decks",
  "A paper Commander deck must never be silently treated as Arena Brawl");


now+=50000;tick();
assert.equal(unwatches,1,"Stale watcher must rearm after no file signals");
assert.equal(watches,2);
assert.match(document.getElementById("status").textContent,/Snapshot not updating/);
now+=4*86400000;
tick();
assert.match(document.getElementById("seasonCountdown").textContent,/Awaiting official date/,
  "Expired schedule must not display a negative countdown");
console.log("Edge snapshot startup, rank rendering and stale rewatch checks passed.");
