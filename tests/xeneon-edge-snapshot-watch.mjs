import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
const source=fs.readFileSync("integrations/xeneon-edge/scripts/widget.js","utf8");
const elements=new Map();
const make=(id="")=>{
  const attrs=new Map(), classes=new Set();
  const node={
    id, textContent:"", value:"", disabled:false, hidden:false, style:{},
    dataset:{}, options:[], children:[],
    classList:{add:c=>classes.add(c),remove:c=>classes.delete(c),
      contains:c=>classes.has(c),toggle:(c,on)=>on?classes.add(c):classes.delete(c)},
    addEventListener:()=>{},
    setAttribute:(k,v)=>attrs.set(k,v),getAttribute:k=>attrs.get(k),
    removeAttribute:k=>attrs.delete(k),
    replaceChildren(...items){this.children=items;this.options=items;},
    appendChild(item){this.children.push(item);}
  };
  return node;
};
const document={
  getElementById(id){if(!elements.has(id))elements.set(id,make(id));return elements.get(id);},
  createElement:()=>make()
};
let listener, tick, watches=0, unwatches=0, now=100000;
const provider={
  fileChanged:{connect(cb){assert.equal(listener,undefined);listener=cb;}},
  watchPath(p){assert.match(p,/streamdeck\.json$/);watches++;},
  unwatchPath(){unwatches++;}
};
const box={
  document,window:{plugins:{Filesystemprovider:provider}},
  localStorage:{getItem:()=>null,setItem(){},removeItem(){}},
  Option:class {constructor(label,value){this.textContent=label;this.value=value;}},
  Date:class extends Date {static now(){return now;}},
  setInterval(fn,ms){assert.equal(ms,5000);tick=fn;},
  pluginFilesystemprovider_initialized:true,
  URL,Intl,console
};
vm.runInNewContext(source,box,{filename:"widget.js"});
assert.equal(watches,1,"Widget must register snapshot watch on startup");
assert.equal(typeof listener,"function");
assert.match(document.getElementById("status").textContent,/Waiting for Arcane Vault heartbeat/);
listener("C:\\Users\\Test\\AppData\\Local\\ArcaneVault\\streamdeck.json",
  JSON.stringify({decks:[],schemaVersion:1,rank:{constructedClass:"Gold",
    constructedLevel:2,limitedClass:"Platinum",limitedLevel:3,
    asOfUtc:"2026-11-18T13:00:00Z"}}));
assert.match(document.getElementById("status").textContent,/Connected, but no saved decks/);
assert.equal(document.getElementById("constructedRank").textContent,"Gold 2");
assert.equal(document.getElementById("limitedRank").textContent,"Platinum 3");
assert.match(document.getElementById("constructedRankIcon").getAttribute("src"),/constructed_gold_t2\.png$/);
now+=50000;tick();
assert.equal(unwatches,1,"Stale watcher must rearm after no file signals");
assert.equal(watches,2);
assert.match(document.getElementById("status").textContent,/Snapshot not updating/);
console.log("Edge snapshot startup, rank rendering and stale rewatch checks passed.");
