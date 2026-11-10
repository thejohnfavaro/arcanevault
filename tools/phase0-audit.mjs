#!/usr/bin/env node
// Arcane Vault Next / Phase 0: read-only backup and sync-snapshot verifier.
// This tool NEVER writes to the source files or reads a live application profile.
import fs from 'node:fs';
import crypto from 'node:crypto';

const MAX_BYTES = 100 * 1024 * 1024;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const has = (obj, field) => Object.prototype.hasOwnProperty.call(obj, field);
const plain = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const norm = value => String(value ?? '').trim().toLowerCase();
const integer = n => Number.isSafeInteger(n) && n >= 0 && n <= 99999;
const fail = (code, message) => { const err = new Error(message); err.code = code; throw err; };

function stable(value) {
  if (Array.isArray(value)) return value.map(stable);
  if (plain(value)) return Object.fromEntries(Object.keys(value).sort()
    .map(k => [k, stable(value[k])]));
  return value;
}
function fingerprint(rows) {
  const canonical = rows.map(row => JSON.stringify(stable(row))).sort();
  return crypto.createHash('sha256').update(JSON.stringify(canonical)).digest('hex');
}
function readDocument(filename) {
  const stat = fs.statSync(filename);
  if (!stat.isFile() || stat.size > MAX_BYTES) fail('INVALID_FILE', 'Input must be a JSON file no larger than 100 MiB.');
  const data = JSON.parse(fs.readFileSync(filename, 'utf8'));
  if (!plain(data)) fail('INVALID_BACKUP', 'Backup must be a JSON object.');
  const sync = data.format === 'arcane-vault-collection-sync' && data.schema === 1;
  const backup = has(data, 'cards') && Array.isArray(data.cards);
  if (!sync && !backup) fail('UNRECOGNIZED_FORMAT', 'Expected an Arcane Vault full backup or schema-1 collection sync snapshot.');
  if (sync && !Array.isArray(data.physicalCards) && !Array.isArray(data.arenaCards))
    fail('INVALID_BACKUP', 'Collection sync snapshot has no collections.');
  return {data, format:sync ? 'collection-sync-v1' : 'full-backup'};
}

function audit(filename) {
  const {data,format} = readDocument(filename);
  const arena = format === 'collection-sync-v1' ? data.arenaCards : data.cards;
  const physical = data.physicalCards;
  const decks = data.deckLibrary;
  const art = data.preferredArtwork;
  const warnings = [];
  const sections = {};
  const counts = {
    arenaNames: null, arenaCopies: null, physicalRows: null,
    physicalNames: null, physicalCopies: null, identifiedRows: null,
    identifiedCopies: null, unidentifiedRows: null, unidentifiedCopies: null,
    duplicatePhysicalRows: null, decks: null, deckRevisions: null,
    preferredArtwork: null
  };

  if (arena !== undefined) {
    if (!Array.isArray(arena) || arena.length > 150000) fail('INVALID_ARENA', 'Arena collection must be an array of at most 150,000 rows.');
    const names = new Set();
    let copies = 0;
    for (const item of arena) {
      if (!plain(item) || typeof item.name !== 'string' || !item.name.trim() || !integer(item.qty))
        fail('INVALID_ARENA', 'Invalid Arena card name or quantity.');
      const name = norm(item.name);
      if (names.has(name)) fail('DUPLICATE_ARENA', 'Duplicate Arena card name detected.');
      names.add(name);
      copies += item.qty;
      if (!Number.isSafeInteger(copies)) fail('INVALID_ARENA', 'Arena copy total overflow.');
    }
    counts.arenaNames = names.size;
    counts.arenaCopies = copies;
    sections.arena = fingerprint(arena);
  }
  if (physical !== undefined) {
    if (!Array.isArray(physical) || physical.length > 150000) fail('INVALID_PHYSICAL', 'Physical inventory must be an array of at most 150,000 rows.');
    const names = new Set();
    const identities = new Set();
    let copies = 0, identifiedRows = 0, identifiedCopies = 0, duplicates = 0;
    for (const item of physical) {
      if (!plain(item) || typeof item.name !== 'string' || !item.name.trim() ||
        !integer(item.qty) || item.qty === 0) fail('INVALID_PHYSICAL', 'Invalid physical card name or quantity.');
      names.add(norm(item.name));
      copies += item.qty;
      if (!Number.isSafeInteger(copies)) fail('INVALID_PHYSICAL', 'Physical copy total overflow.');
      const id = String(item.scryfallId ?? '').trim();
      const confirmed = UUID.test(id) || (!!norm(item.set) && !!norm(item.number));
      if (confirmed) { identifiedRows++; identifiedCopies += item.qty; }
      const identity = [item.name, item.set, item.number, item.finish, item.language,
        item.condition, item.location, item.scryfallId].map(norm).join('|');
      if (identities.has(identity)) duplicates++;
      identities.add(identity);
    }
    Object.assign(counts, {
      physicalRows:physical.length, physicalNames:names.size, physicalCopies:copies,
      identifiedRows, identifiedCopies,
      unidentifiedRows:physical.length - identifiedRows,
      unidentifiedCopies:copies - identifiedCopies,
      duplicatePhysicalRows:duplicates
    });
    if (duplicates) warnings.push('DUPLICATE_PHYSICAL_IDENTITIES');
    if (counts.unidentifiedRows) warnings.push('UNRESOLVED_PHYSICAL_PRINTINGS');
    sections.physical = fingerprint(physical);
  }
  if (decks !== undefined) {
    if (!Array.isArray(decks)) fail('INVALID_DECKS', 'Deck library must be an array.');
    let revisions = 0;
    for (const deck of decks) {
      if (!plain(deck) || typeof deck.name !== 'string' || !Array.isArray(deck.entries))
        fail('INVALID_DECKS', 'Deck library contains an invalid deck.');
      if (deck.revisions !== undefined && !Array.isArray(deck.revisions))
        fail('INVALID_DECKS', 'Deck revisions must be an array.');
      revisions += deck.revisions?.length ?? 0;
    }
    counts.decks = decks.length;
    counts.deckRevisions = revisions;
    sections.decks = fingerprint(decks);
  }
  if (art !== undefined) {
    if (!plain(art)) fail('INVALID_ARTWORK', 'Preferred artwork must be a mapping.');
    counts.preferredArtwork = Object.keys(art).length;
    sections.artwork = fingerprint([art]);
  }
  if (format === 'full-backup' && !has(data, 'physicalCards'))
    warnings.push('BACKUP_DOES_NOT_INCLUDE_PHYSICAL_INVENTORY');
  if (format === 'full-backup' && !has(data, 'deckLibrary'))
    warnings.push('BACKUP_DOES_NOT_INCLUDE_DECK_LIBRARY');

  return {format, counts, warnings, sections};
}

function compare(before, after) {
  const changed = [];
  for (const field of Object.keys(before.counts)) {
    if (before.counts[field] !== after.counts[field]) changed.push(field);
  }
  for (const section of new Set([...Object.keys(before.sections), ...Object.keys(after.sections)])) {
    if (before.sections[section] !== after.sections[section])
      changed.push(section + 'Content');
  }
  return [...new Set(changed)];
}

export function inspectBackup(filename) { return audit(filename); }
export function compareBackups(first, second) { return compare(audit(first), audit(second)); }

function main(args) {
  if (args.length < 2 || args[0] !== '--backup') {
    process.stderr.write('Usage: node tools/phase0-audit.mjs --backup <file.json> [--compare <other.json>]\n');
    return 2;
  }
  const file = args[1];
  const flags = args.slice(2);
  if (flags.length && (flags.length !== 2 || flags[0] !== '--compare'))
    fail('ARGUMENTS', 'Optional argument must be --compare <other.json>.');
  const baseline = audit(file);
  // No card names, decks, paths, print IDs, or content hashes leave this process.
  const report = {tool:'arcane-vault-phase0-audit', readonly:true, format:baseline.format,
    counts:baseline.counts, warnings:baseline.warnings};
  if (flags.length) {
    const newer = audit(flags[1]);
    const changed = compare(baseline, newer);
    report.comparison = {sameFormat:baseline.format === newer.format,
      exactContentMatch:changed.length === 0, changedSections:changed,
      warnings:newer.warnings};
    process.stdout.write(JSON.stringify(report, null, 2) + '\n');
    return changed.length ? 3 : 0;
  }
  process.stdout.write(JSON.stringify(report, null, 2) + '\n');
  return 0;
}
if (process.argv[1] && import.meta.url === new URL('file://' + process.argv[1]).href) {
  try { process.exitCode = main(process.argv.slice(2)); }
  catch (err) {
    // Intentionally redact error object/path to prevent leaking card data from malformed inputs.
    process.stderr.write('Audit failed (' + (err.code || 'INVALID_JSON_OR_INPUT') + '): ' +
      (err.code ? err.message : 'Unable to inspect input file.') + '\n');
    process.exitCode = 2;
  }
}
