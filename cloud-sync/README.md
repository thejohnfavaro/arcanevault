# Arcane Vault Wix Light cloud-sync foundation

**Status:** opt-in transport foundation, not yet connected to the desktop Data & Sync UI or Android peer-merge. No local collections are changed by these files.

## Why two slots?
Wix Light has 1,500 total CMS items; per-device compact snapshots use only two items. Wix CMS also has a 500 KB per-item cap. The endpoint enforces a conservative 250,000-character ceiling; large collections may require chunking or external storage. This is **not** a multi-user backend.

## One-time Wix dashboard setup
1. Open the site for `johnfavaro.com` in Wix, enable **Velo / Dev Mode**.
2. Under CMS create collection **ArcaneVaultDeviceSnapshots** (collection ID must match exactly). Add fields: `deviceId` Text, `revision` Number, `payload` Text, `updatedAt` Date and Time. Set **all visitor/member read and write access to denied**; only site administrators should access the CMS directly.
3. In Wix Secrets Manager, create `ARCANE_VAULT_SYNC_TOKEN` containing a random secret of at least 32 characters. **Do not put this in Git, URL parameters, logs, or screenshots.**
4. Paste `wix/backend/http-functions.js` into the site's **Backend/http-functions.js** (or carefully merge the exports if that file already exists). Publish the site.
5. Visit `https://www.johnfavaro.com/_functions/avSyncHealth`. Expected: JSON with `status: ready`. If the site's canonical URL redirects to a different hostname, use the final HTTPS hostname as the API base.
6. Test with curl (PowerShell users: use `curl.exe`):
   ```sh
   curl -X POST https://www.johnfavaro.com/_functions/avSyncPull -H "Authorization: Bearer YOUR_SECRET" -H "Content-Type: application/json" -d "{}"
   ```
   Initial response should be `{"protocol":1,"slots":[]}`.

## Wire protocol
- `GET /_functions/avSyncHealth`: public operational readiness only.
- `POST /_functions/avSyncPull`: Authorization Bearer token; returns the two independent device slots.
- `POST /_functions/avSyncPush`: Authorization Bearer token; accepts a JSON envelope containing `deviceId` (`windows` or `android`), `expectedRevision` (0 on first push), and `payload` (serialized existing peer snapshot). Returns revision or HTTP 409 conflict.
- **Do not auto-apply received snapshots.** Feed them through the existing peer diff/review and recovery journal. Use the existing baseline to determine changes; a raw slot is neither a cloud-authoritative database nor an instruction to overwrite.
- Credential storage belongs in Windows DPAPI/Credential Manager and Android Keystore-backed encrypted storage; not in settings JSON or app bundles.
- Only the current device may write its own slot. Because Wix CMS does not provide an atomic compare-and-swap here, concurrent clients presenting the same device ID are unsupported.
- This transport has no append-only history and is not a disaster-recovery backup. Keep local snapshots and versioned backups.

## Follow-up integration tasks
Connect the currently saved native peer snapshot export to `WixSyncClient.PushAsync`, then pass the downloaded opposite-device payload to the **existing reviewed merge** process. Add an explicit opt-in Data & Sync button, progress/error states, credential secure-store, tests for 401/409/oversize and interruption, and analogous Android wiring. Do not enable automatic background sync or write-back until safe integration tests pass.
