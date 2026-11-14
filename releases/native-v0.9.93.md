# Arcane Vault Native Windows v0.9.93 — Peer sync preview

## Added
- Export a full Windows/Android peer snapshot from Data & Sync, retaining physical collection printings, Arena ownership, saved decks, artwork preferences and opaque native workspace fields.
- Preview an incoming Android/Windows peer snapshot by stable record identities without changing either local application.
- Advanced three-way comparison using the latest peer file and a previously shared common snapshot. Reports incoming-only changes, local-only changes and concurrent conflicts.
- Native regression coverage for identical snapshots, independent changes, duplicate identities, malformed payloads and unchanged workspaces.

## Changed
- Comparison normalizes JSON object property order to avoid artificial differences between Android and Windows serializers.
- Physical printing keys use length-prefixed fields to avoid ambiguous identity combinations.
- Persist a private stable Windows device UUID for future pairing.

## Fixed
- Conflicting record versions cannot be silently mistaken for a safe whole-workspace replacement.
- Changed or missing records on an incoming peer file are displayed explicitly rather than automatically treated as intentional deletions.

## Removed
- No established functionality removed. The legacy QR/USB collection transfer remains available.

## Safety
- **This is a preview-only sync foundation, not automatic bidirectional synchronization.** No incoming changes are applied. Authenticating paired devices, shared baseline persistence, conflict resolution and atomic cross-device apply remain required before enabling write-back.
- All exports are unencrypted and should be stored privately. Existing native backup/recovery systems are unchanged.
