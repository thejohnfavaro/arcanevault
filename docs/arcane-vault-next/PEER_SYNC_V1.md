# Arcane Vault peer sync v1 — cross-platform exchange foundation

The native Windows and Android applications now share an initial offline exchange contract.

## Portable envelope

```json
{
  "format": "arcane-vault-peer-sync",
  "schema": 1,
  "deviceId": "persistent-guid",
  "revision": "sha256-digest-of-exported-payload",
  "createdUtc": "timestamp",
  "data": {
    "schema": 2,
    "cards": [],
    "physicalCards": [],
    "deckLibrary": [],
    "preferredArtwork": {}
  }
}
```

The Windows exporter preserves all additional fields from the verified native schema-2 workspace, including opaque settings/proxy ledgers. The Android exporter reads both collections from durable native inventory and saved decks/artwork from the original WebView profile; it never fabricates missing printing IDs.

Record match keys:
- Arena: card name, case-normalized (Arena inventory uses name counts)
- Physical: printing ID, card name, set, collector number, finish, language, condition, location, purchase price
- Saved decks: persistent deck ID, not displayed name/commander
- Artwork: existing preference keys

## Current workflow

1. Choose **Export PC / Android Sync** in Windows Data & Sync, or **Export peer sync file** in Android Data & Sync.
2. Copy the resulting JSON file through a user-selected transport (USB file, Files app, LAN transfer, etc.). The file is not encrypted.
3. On the other device, choose **Preview Incoming Peer Sync** and open it.
4. Review additions, differences, and records absent from the incoming file. Existing data remains unchanged.

The viewer does not infer that records absent from a peer were deleted, nor does it choose a winner from timestamps. A safe two-way merge requires proof of a common last-synced baseline, fine-grained tombstones for deliberate deletions, conflict selection and recovery checks. **No automatic apply is enabled in this milestone.**

## Security and integrity

- No cloud account, background server or open LAN listener was added.
- File input capped at 30 MiB with strict format/schema/device identity validation and duplicate-identity rejection.
- Windows native snapshot read checks verified SQLite backup manifest before exporting or comparing.
- Existing Windows recovery and Android backup/rollback workflows remain intact.
- The mobile Play Protect warning is still under investigation. Signed APKs do not prove malware classification.

## Follow-up implementation needed for full bidirectional sync

Implement persisted common-base checkpoint on both devices, per-record three-way merge and deletion tombstones, explicit conflict review, and atomic apply into each platform's authoritative store. Native Windows applies must use the SHA-checked workspace rollback API; Android must synchronize durable collection storage, SQLite mirrors and existing WebView-backed deck/artwork keys with recovery. Authenticate and bind LAN pairing before enabling network transfers. Until these gates pass, do not advertise this as automatic or complete bidirectional sync.
