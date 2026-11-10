#!/usr/bin/env node
// Phase 0 read-only local API latency sampler; never fetches a user's collection export.
// Defaults to Arcane Vault's existing Windows localhost service. No writes or telemetry.
import { performance } from 'node:perf_hooks';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const checks = [
  {name:'catalogStatus', path:'/api/catalog/status', requiresCatalog:false},
  {name:'indexedSearch', path:'/api/catalog/search?scope=all&arena=any&q=sol&size=25&page=0', requiresCatalog:true},
  {name:'setProgress', path:'/api/catalog/sets?arena=any&q=', requiresCatalog:true},
  {name:'printingVariants', path:'/api/catalog/variants?name=Sol%20Ring&scope=all', requiresCatalog:true}
];

function percentile(sorted, rank) {
  return Math.round(sorted[Math.max(0, Math.ceil(rank * sorted.length) - 1)] * 100) / 100;
}
export function summarizeSamples(samples) {
  if (!samples.length) return {count:0, minMs:null, p50Ms:null, p95Ms:null, maxMs:null};
  const values=[...samples].sort((a,b)=>a-b);
  return {count:values.length, minMs:Math.round(values[0]*100)/100,
    p50Ms:percentile(values,.5), p95Ms:percentile(values,.95),
    maxMs:Math.round(values.at(-1)*100)/100};
}

function parseArgs(args) {
  let base='http://127.0.0.1:8765', iterations=20;
  for (let i=0;i<args.length;i++) {
    if (args[i]==='--url' && args[i+1]) base=args[++i];
    else if (args[i]==='--iterations' && args[i+1]) iterations=Number(args[++i]);
    else throw new Error('Usage: node tools/phase0-benchmark.mjs [--url http://127.0.0.1:8765] [--iterations 20]');
  }
  if (!Number.isInteger(iterations) || iterations<3 || iterations>100)
    throw new Error('Iterations must be an integer from 3 to 100.');
  const u=new URL(base);
  if (u.protocol!=='http:' || !['127.0.0.1','localhost','[::1]'].includes(u.hostname)
      || u.username || u.password || u.search || u.hash || u.pathname!=='/')
    throw new Error('Only a local HTTP service root is allowed.');
  return {url:u.toString(),iterations};
}
async function timedGet(base,path) {
  const controller=new AbortController();
  const timeout=setTimeout(()=>controller.abort(),15000);
  const start=performance.now();
  try {
    const response=await fetch(new URL(path,base),{method:'GET',signal:controller.signal,cache:'no-store'});
    const body=await response.text(); // Include transfer + deserialization in the timing.
    const elapsed=performance.now()-start;
    if (!response.ok) throw new Error('GET returned HTTP ' + response.status);
    return {elapsed, json:JSON.parse(body)};
  } finally { clearTimeout(timeout); }
}
export async function measure({url,iterations}) {
  const status=(await timedGet(url,'/api/catalog/status')).json;
  const catalogReady=Number(status.total||0)>0 && !status.busy;
  const result={tool:'arcane-vault-phase0-local-benchmark',readonly:true,
    scope:'read-only localhost API (not UI rendering or a database cold-start test)',
    iterations, catalogReady, tasks:[]};
  for(const check of checks) {
    if (check.requiresCatalog && !catalogReady) {
      result.tasks.push({name:check.name,status:'skipped-no-ready-catalog'});
      continue;
    }
    const timings=[];
    try {
      await timedGet(url,check.path); // warmup
      for(let i=0;i<iterations;i++) timings.push((await timedGet(url,check.path)).elapsed);
      result.tasks.push({name:check.name,status:'ok',...summarizeSamples(timings)});
    } catch(err) {
      result.tasks.push({name:check.name,status:'error',detail:err.name==='AbortError'?'request timeout':'HTTP/JSON request failed'});
    }
  }
  return result;
}
if (process.argv[1] && import.meta.url===pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const args=parseArgs(process.argv.slice(2));
    const result=await measure(args);
    process.stdout.write(JSON.stringify(result,null,2)+'\n');
    if (result.tasks.some(t=>t.status==='error'))process.exitCode=3;
  } catch(e) {
    process.stderr.write('Benchmark failed: '+(e.code==='ECONNREFUSED'?'Arcane Vault is not running on that localhost address.':e.message)+'\n');
    process.exitCode=2;
  }
}
