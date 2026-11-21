// Deploy this file as Wix Velo Backend/http-functions.js.
// Private CMS collection: ArcaneVaultDeviceSnapshots
// Fields: deviceId (Text), revision (Number), payload (Text), updatedAt (Date and Time)
// Secret Manager key: ARCANE_VAULT_SYNC_TOKEN (random, at least 32 bytes).
import { ok, badRequest, forbidden, serverError, response } from 'wix-http-functions';
import wixData from 'wix-data';
import { getSecret } from 'wix-secrets-backend';

const COLLECTION = 'ArcaneVaultDeviceSnapshots';
const MAX_CHARS = 250000; // bounded below the Wix 500 KB item cap
const DEVICES = new Set(['windows', 'android']);
const dataOptions = { suppressAuth: true, consistentRead: true };

function json(status, value) {
  return response({ status, headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' },
    body: JSON.stringify(value) });
}

async function authorized(request) {
  const secret = await getSecret('ARCANE_VAULT_SYNC_TOKEN');
  const header = request.headers.authorization || request.headers.Authorization || '';
  return typeof secret === 'string' && secret.length >= 32 &&
    header === 'Bearer ' + secret;
}

async function parse(request) {
  const input = await request.body.json();
  if (!input || typeof input !== 'object' || Array.isArray(input)) throw new Error('invalid JSON object');
  return input;
}

async function readSlot(device) {
  try {
    const row = await wixData.get(COLLECTION, device, dataOptions);
    return row ? { deviceId: device, revision: row.revision || 0,
      payload: row.payload || '', updatedAt: row.updatedAt || null } : null;
  } catch (error) {
    // A missing item is normal; database permission or connectivity errors are not.
    if (error && (error.code === 'WDE0025' || /does not exist|not found/i.test(error.message || ''))) return null;
    throw error;
  }
}

export function get_avSyncHealth() {
  return json(200, { service: 'arcane-vault-sync', version: 1, status: 'ready' });
}

// POST { } -> latest two separate device snapshots (no server-side merge).
export async function post_avSyncPull(request) {
  try {
    if (!(await authorized(request))) return json(401, { error: 'unauthorized' });
    const slots = await Promise.all(['windows', 'android'].map(readSlot));
    return json(200, { protocol: 1, slots: slots.filter(Boolean) });
  } catch (error) {
    console.error('Arcane Vault sync pull failed', error);
    return json(500, { error: 'sync_unavailable' });
  }
}

// POST { deviceId, expectedRevision, payload }.
// A cloud slot belongs to one device. No implicit deletions or cross-device overwrites.
// This revision check does NOT provide atomic CAS; avoid concurrent uploads from one device.
export async function post_avSyncPush(request) {
  try {
    if (!(await authorized(request))) return json(401, { error: 'unauthorized' });
    const input = await parse(request);
    if (!DEVICES.has(input.deviceId) || !Number.isSafeInteger(input.expectedRevision) ||
        input.expectedRevision < 0 || typeof input.payload !== 'string' ||
        !input.payload || input.payload.length > MAX_CHARS) {
      return json(400, { error: 'invalid_payload', maxCharacters: MAX_CHARS });
    }
    // Ensure the snapshot is syntactically a JSON object; it is not yet a trusted merge.
    let decoded;
    try { decoded = JSON.parse(input.payload); } catch (_) { return json(400, { error: 'invalid_snapshot_json' }); }
    if (!decoded || typeof decoded !== 'object' || Array.isArray(decoded)) return json(400, { error: 'invalid_snapshot_json' });
    const current = await readSlot(input.deviceId);
    const revision = current ? current.revision : 0;
    if (revision !== input.expectedRevision)
      return json(409, { error: 'revision_conflict', currentRevision: revision });
    const next = revision + 1;
    await wixData.save(COLLECTION, { _id: input.deviceId, deviceId: input.deviceId,
      revision: next, payload: input.payload, updatedAt: new Date() }, dataOptions);
    return json(200, { deviceId: input.deviceId, revision: next });
  } catch (error) {
    if (/invalid JSON object|Unexpected token/i.test(error.message || ''))
      return json(400, { error: 'invalid_request' });
    console.error('Arcane Vault sync push failed', error);
    return json(500, { error: 'sync_unavailable' });
  }
}
