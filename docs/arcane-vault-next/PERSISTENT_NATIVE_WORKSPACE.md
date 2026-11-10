# AVN-020 — Persistent native Windows workspace (first bootstrap slice)

**Purpose:** Remove the repeated "backup JSON + catalog chooser" cradle, without taking irreversible ownership of the existing installation.

### Source and ownership boundaries

- Installed legacy Windows data lives in `%LOCALAPPDATA%\\ArcaneVault\\` and the legacy WebView profile. This slice **never alters, opens for writing, or auto-imports from that profile**.
- An explicitly selected full schema-2 JSON backup is imported **once** into `%LOCALAPPDATA%\\ArcaneVaultNext\\native-workspace.sqlite`, using a sibling temporary SQLite database, a single transaction, source SHA-256, row-level readback, SQLite integrity check, and an atomic no-overwrite move.
- Source bytes are preserved inside the native SQLite manifest, alongside typed Arena and physical inventory rows and preserved deck + artwork payloads. Reopen validates row/quantity/deck/artwork agreement with the original document; corruption causes an error, **never a silent reset or overwrite**.
- An explicitly selected Scryfall catalog can be copied into `%LOCALAPPDATA%\\ArcaneVaultNext\\native-catalog.sqlite` using SQLite's consistent online backup, verified before publishing. This catalog is independent from the old app's catalog and is re-opened read-only on startup without another temporary copy.
- On future launches, the native collection and saved native catalog reopen directly. The Windows native screens (physical/Arena, details and paper Set Progress) now use these persisted files without a new export.

### User acceptance walkthrough

1. Download/build the newest **ArcaneVaultNext-NativeUI-SPIKE** artifact (a separate preview, not a signed release).
2. In the old Windows app, export a full schema-2 backup. Retain it privately.
3. In the native app, **Open a read-only catalog snapshot**, then **Keep catalog natively**.
4. **Import backup once**, confirm, and select the full backup.
5. Quit and restart Arcane Vault Next. The native collection view opens from the private SQLite workspace automatically; no chooser is required.
6. Check physical/Arena copy totals, decks/revisions/artwork counts, card details and verified Set Progress against the previous preview. The old app must still load untouched.

### Failure and data preservation tests

Synthetic native regression validates first import, close/reopen, repeated import refusal, malformed backup failure without published files, row tampering detection, SHA preservation of original JSON backup, and independent catalog copy/reopen without touching the old catalog. CI is **not** evidence of end-to-end user-device safety: do not overwrite installed Arcane Vault or remove source backup.

### Scope still outstanding

- No native authoritative writes, inventory editing, automatic upgrades/migrations, data refresh/reimport, installed WebView profile reader, signed updater integration, concurrent legacy/native sync, cloud sync, scanner or Android changes.
- Native SQLite contains unencrypted local collection contents; it should stay in the user's private Windows profile. Always keep full backups.
- Do not resume EDH Lab until the Windows-first core recovery gate is complete.
